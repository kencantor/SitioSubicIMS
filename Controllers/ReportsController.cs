using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SitioSubicIMS.Web.Data;
using SitioSubicIMS.Web.Models;
using SitioSubicIMS.Web.Models.ViewModels;
using iTextSharp.text;
using iTextSharp.text.pdf;
using SitioSubicIMS.Web.Helpers;
using System.IO;
using Microsoft.Extensions.Hosting;

namespace SitioSubicIMS.Web.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public ReportsController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        public async Task<IActionResult> Index()
        {
            var totalDueAmount = await _context.Billings
                .Include(p => p.Payments)
                .Include(b => b.Reading)
                .ThenInclude(r => r.Meter)
                .Where(b => b.BillingStatus == BillingStatus.Unpaid || b.BillingStatus == BillingStatus.Pending)
                .AsNoTracking()
                .ToListAsync();

            var sumDue = totalDueAmount
                .Sum(b => (b.DueDate > DateTime.Now ? b.DueAmount : b.OverDueAmount) - b.Payments.Sum(a => a.AmountPaid));

            var pastdues = totalDueAmount.Count(b => b.DueDate < DateTime.Now);

            // Calculate last 6 months including current month
            var sixMonthsAgo = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddMonths(-5);

            var billingData = await _context.Billings
                .Include(b => b.Payments)
                .Include(b => b.Reading)
                .Where(b => b.Reading.BillingYear * 100 + b.Reading.BillingMonth >= sixMonthsAgo.Year * 100 + sixMonthsAgo.Month)
                .AsNoTracking()
                .ToListAsync();

            // Group by year/month
            var grouped = billingData
                .GroupBy(b => new { b.Reading.BillingYear, b.Reading.BillingMonth })
                .OrderBy(g => g.Key.BillingYear).ThenBy(g => g.Key.BillingMonth)
                .ToList();

            var labels = new List<string>();
            var monthlyBillings = new List<decimal>();
            var monthlyPayments = new List<decimal>();

            for (int i = 0; i < 6; i++)
            {
                var month = sixMonthsAgo.AddMonths(i);
                var group = grouped.FirstOrDefault(g => g.Key.BillingYear == month.Year && g.Key.BillingMonth == month.Month);

                labels.Add(month.ToString("MMM yyyy"));

                if (group != null)
                {
                    monthlyBillings.Add(group.Sum(b => b.DueAmount)); // sum billed for that month
                    monthlyPayments.Add(group.Sum(b => b.Payments.Sum(p => p.AmountPaid))); // sum payments collected
                }
                else
                {
                    monthlyBillings.Add(0);
                    monthlyPayments.Add(0);
                }
            }

            var reportData = new ReportDashboardViewModel
            {
                TotalAccounts = await _context.Accounts.Where(a => a.IsActive).CountAsync(),
                TotalBillings = sumDue,
                UnpaidBillingsCount = totalDueAmount.Count,
                TotalPayments = await _context.Payments.SumAsync(p => p.AmountPaid),
                PastDues = pastdues,
                ChartLabels = labels,
                MonthlyBillings = monthlyBillings,
                MonthlyPayments = monthlyPayments
            };

            return View(reportData);
        }

        public async Task<IActionResult> GetActiveAccountsModal()
        {
            var activeAccounts = await _context.Accounts
                .Include(a => a.Meter)
                .Where(a => a.IsActive)
                .OrderBy(a => a.AccountName)
                .AsNoTracking()
                .ToListAsync();

            return PartialView("Partials/_ActiveAccountsModal", activeAccounts);
        }
        public async Task<IActionResult> GetUnpaidBillingsModal()
        {
            var unpaidBillings = await _context.Billings
                .Include(b => b.Payments)
                .Include(b => b.Reading)
                    .ThenInclude(r => r.Meter)
                .Where(b => b.BillingStatus == BillingStatus.Unpaid || b.BillingStatus == BillingStatus.Pending)
                .AsNoTracking()
                .ToListAsync();

            var accounts = await _context.Accounts
                .Where(a => a.IsActive)
                .AsNoTracking()
                .ToListAsync();

            var data = unpaidBillings.Select(b =>
            {
                var account = accounts.FirstOrDefault(a => a.MeterID == b.Reading.MeterID);

                return new UnpaidBillingViewModel
                {
                    AccountName = account?.AccountName ?? "[Unknown]",
                    BillingNumber = b.BillingNumber,
                    MeterNumber = b.Reading.Meter?.MeterNumber ?? "[N/A]",
                    Period = new DateTime(b.Reading.BillingYear, b.Reading.BillingMonth, 1).ToString("MMMM yyyy"),
                    DueAmount = b.DueDate > DateTime.Now ? b.DueAmount : b.OverDueAmount,
                    Paid = b.Payments.Sum(p => p.AmountPaid),
                    Balance = (b.DueDate > DateTime.Now ? b.DueAmount : b.OverDueAmount) - b.Payments.Sum(p => p.AmountPaid)
                };
            }).ToList();

            return PartialView("Partials/_UnpaidBillingsModal", data);
        }
        public async Task<IActionResult> GetPaymentsCollectedModal()
        {
            var payments = await _context.Payments
                .Include(p => p.Billing)
                    .ThenInclude(b => b.Reading)
                .AsNoTracking()
                .OrderByDescending(p => p.PaymentDate)
                .ToListAsync();

            var accounts = await _context.Accounts.AsNoTracking().ToListAsync();

            var data = payments.Select(p =>
            {
                var billing = p.Billing;
                var reading = billing?.Reading;
                var account = accounts.FirstOrDefault(a => a.MeterID == reading?.MeterID);

                return new PaymentsCollectedViewModel
                {
                    AccountName = account?.AccountName ?? "[Unknown]",
                    PaymentNumber = p?.PaymentNumber ?? "N/A",
                    BillingNumber = billing?.BillingNumber ?? "[N/A]",
                    PaymentDate = p.PaymentDate,
                    PaymentMethod = p.PaymentMethod.ToString(),
                    AmountPaid = p.AmountPaid
                };
            }).ToList();

            return PartialView("Partials/_PaymentsCollectedModal", data);
        }
        public async Task<IActionResult> GetPastDuesModal()
        {
            var pastDues = await _context.Billings
                .Include(b => b.Payments)
                .Include(b => b.Reading)
                    .ThenInclude(r => r.Meter)
                .Where(b =>
                    (b.BillingStatus == BillingStatus.Unpaid || b.BillingStatus == BillingStatus.Pending) &&
                    b.DueDate < DateTime.Now)
                .AsNoTracking()
                .ToListAsync();

            var accounts = await _context.Accounts
                .Where(a => a.IsActive)
                .AsNoTracking()
                .ToListAsync();

            var data = pastDues.Select(b =>
            {
                var account = accounts.FirstOrDefault(a => a.MeterID == b.Reading.MeterID);
                var paidAmount = b.Payments.Sum(p => p.AmountPaid);
                var balance = b.OverDueAmount - paidAmount;
                var dueDate = b.DueDate;
                var daysOverdue = (DateTime.Now - dueDate).Days;

                return new PastDueViewModel
                {
                    AccountName = account?.AccountName ?? "[Unknown]",
                    BillingNumber = b.BillingNumber,
                    MeterNumber = b.Reading.Meter?.MeterNumber ?? "[N/A]",
                    BillingPeriod = new DateTime(b.Reading.BillingYear, b.Reading.BillingMonth, 1).ToString("MMMM yyyy"),
                    DueDate = dueDate,
                    DaysOverdue = daysOverdue,
                    OverDueAmount = b.OverDueAmount,
                    Paid = paidAmount,
                    Balance = balance
                };
            }).ToList();

            return PartialView("Partials/_PastDuesModal", data);
        }
        public IActionResult PrintAccountsList()
        {
            var accounts = _context.Accounts
                .OrderBy(a => a.AccountName)
                .ToList();

            using (var ms = new MemoryStream())
            {
                // Create a new PDF document
                Document doc = new Document(PageSize.A4, 36, 36, 36, 36);
                PdfWriter.GetInstance(doc, ms);
                doc.Open();

                // Add reusable header
                var headerTable = PdfHelper.CreateHeader(_env.WebRootPath);
                doc.Add(headerTable);

                // Add report title
                var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16);
                var normalFont = FontFactory.GetFont(FontFactory.HELVETICA, 12);
                var smallFont = FontFactory.GetFont(FontFactory.HELVETICA, 9);

                Paragraph title = new Paragraph("Accounts List Report", titleFont)
                {
                    SpacingBefore = 10f,
                    SpacingAfter = 10f,
                    Alignment = Element.ALIGN_CENTER
                };
                doc.Add(title);

                // Generation date
                Paragraph genDate = new Paragraph("Generated on: " + DateTime.Now.ToString("MMMM dd, yyyy HH:mm"), normalFont)
                {
                    SpacingAfter = 15f,
                    Alignment = Element.ALIGN_CENTER
                };
                doc.Add(genDate);

                // Add summary
                Paragraph summary = new Paragraph($"Total Accounts: {accounts.Count}", normalFont)
                {
                    SpacingAfter = 15f,
                    Alignment = Element.ALIGN_LEFT
                };
                doc.Add(summary);

                // Create table with columns for account details (example: Account Number, Name, Address)
                PdfPTable table = new PdfPTable(4)
                {
                    WidthPercentage = 100
                };
                table.SetWidths(new float[] { 15f, 20f, 20f, 45f });

                // Table header row
                var headerCellFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10);
                table.AddCell(new PdfPCell(new Phrase("Account No.", headerCellFont)));
                table.AddCell(new PdfPCell(new Phrase("Name", headerCellFont)));
                table.AddCell(new PdfPCell(new Phrase("Contact No.", headerCellFont)));
                table.AddCell(new PdfPCell(new Phrase("Address", headerCellFont)));

                // Add account data rows
                foreach (var account in accounts)
                {
                    table.AddCell(new PdfPCell(new Phrase(account.AccountNumber ?? "---", smallFont)));
                    table.AddCell(new PdfPCell(new Phrase(account.AccountName ?? "---", smallFont)));
                    table.AddCell(new PdfPCell(new Phrase(account.ContactNumber ?? "---", smallFont)));
                    table.AddCell(new PdfPCell(new Phrase(account.Address ?? "---", smallFont)));
                }

                doc.Add(table);

                doc.Close();

                byte[] bytes = ms.ToArray();
                return File(bytes, "application/pdf");

            }
        }
        public IActionResult PrintMonthlyBillingList(string billingPeriod)
        {
            if (string.IsNullOrEmpty(billingPeriod))
                return BadRequest("Billing period is required.");

            // Parse billingPeriod input (format: "yyyy-MM")
            if (!DateTime.TryParse(billingPeriod + "-01", out DateTime periodDate))
                return BadRequest("Invalid billing period format.");

            string monthYearText = periodDate.ToString("MMMM yyyy");

            // TODO: Query your billing data filtered by periodDate's month & year
            var billings = _context.Billings
                .Include(b => b.Payments)
                .Include(b => b.Reading)
                    .ThenInclude(r => r.Meter)
                .Where(b => b.Reading.BillingMonth == periodDate.Month && b.Reading.BillingYear == periodDate.Year)
                .AsNoTracking()
                .ToList();

            var accounts = _context.Accounts
                .Where(a => a.IsActive)
                .AsNoTracking()
                .ToList();

            // Generate PDF
            using var ms = new MemoryStream();
            Document document = new Document(PageSize.A4, 36, 36, 36, 36);
            PdfWriter.GetInstance(document, ms);
            document.Open();

            // Add header
            var headerTable = PdfHelper.CreateHeader(_env.WebRootPath);
            document.Add(headerTable);

            // Report Title
            var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16);
            var subTitleFont = FontFactory.GetFont(FontFactory.HELVETICA, 12);
            Paragraph title = new Paragraph($"Monthly Billing Report - {monthYearText}", titleFont)
            {
                Alignment = Element.ALIGN_CENTER,
                SpacingAfter = 10f
            };
            document.Add(title);

            Paragraph generatedOn = new Paragraph($"Generated on: {DateTime.Now:MMMM dd, yyyy}", subTitleFont)
            {
                Alignment = Element.ALIGN_CENTER,
                SpacingAfter = 15f
            };
            document.Add(generatedOn);

            // Summary info
            int totalAccountsBilled = billings.Count;
            decimal totalAmountBilled = billings.Sum(b => b.DueAmount);

            Paragraph summary = new Paragraph($"Total Accounts Billed: {totalAccountsBilled}\nTotal Amount Billed: ₱{totalAmountBilled:N2}", subTitleFont)
            {
                Alignment = Element.ALIGN_LEFT,
                SpacingAfter = 15f
            };
            document.Add(summary);

            // Billing Details Table
            PdfPTable table = new PdfPTable(6) { WidthPercentage = 100 };
            table.SetWidths(new float[] { 15f, 25f, 15f, 15f, 15f, 15f });

            // Table header
            var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10);
            table.AddCell(new PdfPCell(new Phrase("Account No.", headerFont)));
            table.AddCell(new PdfPCell(new Phrase("Account Name", headerFont)));
            table.AddCell(new PdfPCell(new Phrase("Billing Date", headerFont)));
            table.AddCell(new PdfPCell(new Phrase("Amount Due", headerFont)));
            table.AddCell(new PdfPCell(new Phrase("Over Due", headerFont)));
            table.AddCell(new PdfPCell(new Phrase("Status", headerFont)));

            var bodyFont = FontFactory.GetFont(FontFactory.HELVETICA, 10);
            // Join billings with accounts based on MeterID
            var billingWithAccounts = (from b in billings
                                       join a in accounts on b.Reading.MeterID equals a.MeterID
                                       select new
                                       {
                                           Billing = b,
                                           Account = a
                                       })
                                       .OrderBy(x => x.Account.AccountName) // Sort by account name
                                       .ToList();
            // Use the joined list in your PDF generation loop
            foreach (var item in billingWithAccounts)
            {
                var billing = item.Billing;
                var account = item.Account;

                table.AddCell(new PdfPCell(new Phrase(account.AccountNumber.ToString(), bodyFont)));
                table.AddCell(new PdfPCell(new Phrase(account.AccountName ?? "-", bodyFont)));
                table.AddCell(new PdfPCell(new Phrase(billing.BillingDate.ToString("MM/dd/yyyy"), bodyFont)));
                var dueAmountCell = new PdfPCell(new Phrase($"₱{billing.DueAmount:N2}", bodyFont))
                {
                    HorizontalAlignment = Element.ALIGN_RIGHT
                };
                table.AddCell(dueAmountCell);

                var overDueAmountCell = new PdfPCell(new Phrase($"₱{billing.OverDueAmount:N2}", bodyFont))
                {
                    HorizontalAlignment = Element.ALIGN_RIGHT
                };
                table.AddCell(overDueAmountCell);

                table.AddCell(new PdfPCell(new Phrase(billing.BillingStatus.ToString(), bodyFont)));
            }

            document.Add(table);
            document.Close();

            byte[] bytes = ms.ToArray();
            return File(bytes, "application/pdf");
        }
        [HttpGet]
        public IActionResult PrintPaymentList(string billingPeriod)
        {
            if (!DateTime.TryParse($"{billingPeriod}-01", out var periodDate))
            {
                return BadRequest("Invalid billing period format.");
            }

            // Query payments filtered by billing period month/year
            var payments = _context.Payments
                .Include(p => p.Billing)
                    .ThenInclude(b => b.Reading)
                        .ThenInclude(r => r.Meter)
                .Where(p => p.PaymentDate.Month == periodDate.Month && p.PaymentDate.Year == periodDate.Year)
                .AsNoTracking()
                .ToList();

            // Get accounts to link if needed (optional)
            var accounts = _context.Accounts
                .Where(a => a.IsActive)
                .AsNoTracking()
                .ToList();

            // Sort payments by account name (join with accounts if necessary)
            var paymentsWithAccount = payments
                .Select(p =>
                {
                    var account = accounts.FirstOrDefault(a => a.MeterID == p.Billing.Reading.MeterID);
                    return new
                    {
                        Payment = p,
                        Account = account
                    };
                })
                .OrderBy(x => x.Account?.AccountName)
                .ToList();

            using (var ms = new MemoryStream())
            {
                // Create document and writer
                var document = new Document(PageSize.A4, 36, 36, 36, 36);
                var writer = PdfWriter.GetInstance(document, ms);
                document.Open();

                // Add header from your helper
                var headerTable = PdfHelper.CreateHeader(_env.WebRootPath);
                document.Add(headerTable);

                // Report title & generation date
                var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14);
                var bodyFont = FontFactory.GetFont(FontFactory.HELVETICA, 10);

                var reportTitle = new Paragraph($"Payment List Report for {periodDate.ToString("MMMM yyyy")}", titleFont)
                {
                    Alignment = Element.ALIGN_CENTER,
                    SpacingAfter = 15f
                };
                document.Add(reportTitle);

                var generationDate = new Paragraph($"Generated on: {DateTime.Now:MM/dd/yyyy}", bodyFont)
                {
                    Alignment = Element.ALIGN_CENTER,
                    SpacingAfter = 20f
                };
                document.Add(generationDate);

                // Create table with columns: Account Number, Account Name, Payment Date, Amount Paid, Payment Method (example)
                float[] widths = { 15f, 20f, 15f, 13f, 13f, 14f };
                var table = new PdfPTable(widths)
                {
                    WidthPercentage = 100,
                    SpacingBefore = 10f
                };
                table.SetWidths(widths);

                // Add table headers
                string[] headers = { "Account No.", "Name", "Payment No.", "Date", "Amount", "Method" };
                foreach (var header in headers)
                {
                    var cell = new PdfPCell(new Phrase(header, bodyFont))
                    {
                        BackgroundColor = BaseColor.LIGHT_GRAY,
                        HorizontalAlignment = Element.ALIGN_CENTER,
                        Padding = 5
                    };
                    table.AddCell(cell);
                }

                // Add payment data rows
                foreach (var item in paymentsWithAccount)
                {
                    var account = item.Account;
                    var payment = item.Payment;

                    table.AddCell(new PdfPCell(new Phrase(account?.AccountNumber ?? "-", bodyFont)));
                    table.AddCell(new PdfPCell(new Phrase(account?.AccountName ?? "-", bodyFont)));
                    table.AddCell(new PdfPCell(new Phrase(payment.PaymentNumber ?? "-", bodyFont)));
                    table.AddCell(new PdfPCell(new Phrase(payment.PaymentDate.ToString("MM/dd/yyyy"), bodyFont)));

                    var amountCell = new PdfPCell(new Phrase($"₱{payment.AmountPaid:N2}", bodyFont))
                    {
                        HorizontalAlignment = Element.ALIGN_RIGHT
                    };
                    table.AddCell(amountCell);

                    table.AddCell(new PdfPCell(new Phrase(payment.PaymentMethod.ToString() ?? "-", bodyFont)));
                }

                document.Add(table);

                // Add totals summary
                var totalPayments = paymentsWithAccount.Count;
                var totalAmountPaid = paymentsWithAccount.Sum(x => x.Payment.AmountPaid);

                var summary = new Paragraph($"\nTotal No. of Payments: {totalPayments}\nTotal Amount Collected: ₱{totalAmountPaid:N2}", bodyFont)
                {
                    Alignment = Element.ALIGN_RIGHT,
                    SpacingBefore = 20f
                };
                document.Add(summary);

                document.Close();

                byte[] bytes = ms.ToArray();
                return File(bytes, "application/pdf");
            }
        }
    }
}
