using iTextSharp.text.pdf;
using iTextSharp.text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SitioSubicIMS.Web.Data;
using SitioSubicIMS.Web.Helpers;
using SitioSubicIMS.Web.Models;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _context;

    public HomeController(ApplicationDbContext context)
    {
        _context = context;
    }

    public IActionResult Index()
    {
        if (User.Identity != null && User.Identity.IsAuthenticated && User.Claims.Any(c => c.Type == System.Security.Claims.ClaimTypes.Role))
        {
            return RedirectToAction("Index", "Dashboard");
        }

        return View();
    }

    [HttpGet]
    public async Task<IActionResult> GetAccountSummary(string accountNumber)
    {
        if (string.IsNullOrEmpty(accountNumber))
        {
            return Json(new { error = "Invalid account number" });
        }

        var account = await _context.Accounts
            .Include(a => a.Meter)
            .FirstOrDefaultAsync(a => a.AccountNumber == accountNumber && a.IsActive);

        if (account == null)
        {
            return Json(null); // Triggers "Account not found" in JS
        }

        var billings = await _context.Billings
            .Where(b => b.Reading.MeterID == account.MeterID)
            .Include(b => b.Reading)
            .OrderByDescending(b => b.BillingDate)
            .Take(5)
            .Select(b => new
            {
                BillingNumber = b.BillingNumber,
                BillingID = b.BillingID,
                BillingDate = b.BillingDate.ToString("MMMM yyyy"),
                BaseAmount = b.BaseAmount.ToString("N2"),
                DueAmount = b.DueAmount.ToString("N2"),
                BillingStatus = b.BillingStatus.ToString()
            })
            .ToListAsync();

        var payments = await _context.Payments
            .Where(p => p.Billing.Reading.MeterID == account.MeterID)
            .Include(p => p.Billing)
            .OrderByDescending(p => p.PaymentDate)
            .Take(6)
            .Select(p => new
            {
                PaymentNumber = p.PaymentNumber,
                PaymentDate = p.PaymentDate.ToString("MMM-dd-yyyy"),
                AmountPaid = p.AmountPaid.ToString("N2"),
                PaymentMethod = p.PaymentMethod.ToString()
            })
            .ToListAsync();

        return Json(new
        {
            AccountName = account.AccountName,
            ContactNumber = account.ContactNumber,
            Address = account.Address,
            MeterNumber = account.Meter?.MeterNumber ?? "N/A",
            Billings = billings,
            Payments = payments
        });
    }

    [HttpGet]
    public async Task<IActionResult> ViewBillingStatement(int id)
    {
        var billing = await _context.Billings
            .Include(b => b.Reading)
                .ThenInclude(r => r.Meter)
            .FirstOrDefaultAsync(b => b.BillingID == id);
        if (billing == null)
        {
            return NotFound();
        }

        // Get MeterID from billing
        var meterId = billing.Reading?.Meter?.MeterID;

        var previousBillings = await _context.Billings
            .Include(b => b.Reading)
            .Where(b =>
            b.IsActive &&
            b.Reading.MeterID == meterId &&
            b.BillingID != billing.BillingID)
            .OrderByDescending(b => b.BillingDate)
            .Take(5)
            .ToListAsync();


        Account account = null;
        if (meterId != null)
        {
            account = await _context.Accounts
                .FirstOrDefaultAsync(a => a.MeterID == meterId);
        }

        using (var memoryStream = new MemoryStream())
        {
            // Set document size to A4
            var document = new Document(PageSize.A4, 36, 36, 36, 36); // optional set margins
            var writer = PdfWriter.GetInstance(document, memoryStream);
            document.Open();

            // Add header
            string wwwRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var header = PdfHelper.CreateHeader(wwwRootPath);
            document.Add(header);

            // Add Document Title
            var billingDate = new DateTime(billing.Reading.BillingYear, billing.Reading.BillingMonth, 1);
            var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14);
            var subtitleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12);
            var labelFont = FontFactory.GetFont(FontFactory.HELVETICA, 10);
            var valueFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10);
            var smallFont = FontFactory.GetFont(FontFactory.HELVETICA, 9);

            var title = new Paragraph($"Billing Statement for {billingDate:MMM yyyy}", titleFont)
            {
                Alignment = Element.ALIGN_CENTER,
                SpacingAfter = 4f
            };
            document.Add(title);

            // Generated timestamp
            var generated = new Paragraph($"Generated: {DateTime.Now:MMMM dd, yyyy hh:mm tt}", labelFont)
            {
                Alignment = Element.ALIGN_CENTER,
                SpacingAfter = 10f
            };
            document.Add(generated);


            // Define light gray color
            BaseColor lightGray = new BaseColor(200, 200, 200);
            BaseColor lightBlue = new BaseColor(224, 242, 255);


            // Helper method to create a cell
            PdfPCell CreateCell(string text, Font font, int colspan = 1)
            {
                return new PdfPCell(new Phrase(text, font))
                {
                    Colspan = colspan,
                    BorderColor = lightGray,
                    Padding = 4f
                };
            }

            // Create Account Details Table
            float[] tableWidths = new float[] { 15f, 35f, 15f, 35f };
            PdfPTable accountTable = new PdfPTable(4)
            {
                WidthPercentage = 100
            };
            accountTable.SetWidths(tableWidths);
            PdfPCell headerCell = CreateCell("Account Details", subtitleFont, 4);
            headerCell.BackgroundColor = lightBlue;
            accountTable.AddCell(headerCell);
            accountTable.AddCell(CreateCell("Name", labelFont));
            accountTable.AddCell(CreateCell(account?.AccountName ?? "-", valueFont));
            accountTable.AddCell(CreateCell("Meter No.", labelFont));
            accountTable.AddCell(CreateCell(billing?.Reading?.Meter?.MeterNumber ?? "-", valueFont));
            accountTable.AddCell(CreateCell("Account No.", labelFont));
            accountTable.AddCell(CreateCell(account?.AccountNumber ?? "-", valueFont));
            accountTable.AddCell(CreateCell("Contact No.", labelFont));
            accountTable.AddCell(CreateCell(account?.ContactNumber ?? "-", valueFont));
            accountTable.AddCell(CreateCell("Address", labelFont));
            accountTable.AddCell(CreateCell(account?.Address + " Sitio Subic, Brgy. Balele Tanauan City, Batangas" ?? "-", valueFont, 3));
            document.Add(accountTable);
            document.Add(new Paragraph("\n")); // Optional spacer

            // Get current reading info
            var currentReading = billing.Reading;
            DateTime? periodTo = currentReading?.ReadingDate;

            // Get previous reading for the same meter, if any
            DateTime? periodFrom = await _context.Readings
                .Where(r => r.MeterID == currentReading.MeterID && r.ReadingDate < currentReading.ReadingDate)
                .OrderByDescending(r => r.ReadingDate)
                .Select(r => (DateTime?)r.ReadingDate)
                .FirstOrDefaultAsync();

            // If no previous reading found, fallback to meter creation date
            if (periodFrom == null)
            {
                periodFrom = currentReading?.Meter?.DateCreated;
            }

            // Create Billing Details Table
            PdfPTable billingTable = new PdfPTable(4)
            {
                WidthPercentage = 100
            };
            billingTable.SetWidths(tableWidths);

            // Header
            PdfPCell billingHeaderCell = CreateCell("Billing Details", subtitleFont, 4);
            billingHeaderCell.BackgroundColor = lightBlue;
            billingTable.AddCell(billingHeaderCell);

            // Period From and To
            string periodDisplay = (periodFrom?.ToString("MMMM dd, yyyy") ?? "-") + " - " + (periodTo?.ToString("MMMM dd, yyyy") ?? "-");
            billingTable.AddCell(CreateCell("Billing No.", labelFont));
            billingTable.AddCell(CreateCell(billing.BillingNumber, valueFont));
            billingTable.AddCell(CreateCell("Billing Date", labelFont));
            billingTable.AddCell(CreateCell(billing.BillingDate.ToString("MMMM dd, yyyy"), valueFont));

            billingTable.AddCell(CreateCell("Period", labelFont));
            billingTable.AddCell(CreateCell(periodDisplay, valueFont));
            billingTable.AddCell(CreateCell("Due Date", labelFont));
            billingTable.AddCell(CreateCell(billing.DueDate.ToString("MMMM dd, yyyy") ?? "-", valueFont));

            billingTable.AddCell(CreateCell("Prev. Reading", labelFont));
            billingTable.AddCell(CreateCell(billing.Reading.PreviousReadingValue.ToString("N0"), valueFont));
            billingTable.AddCell(CreateCell("Curr. Reading", labelFont));
            billingTable.AddCell(CreateCell(billing.Reading.ReadingValue.ToString("N0"), valueFont));

            billingTable.AddCell(CreateCell("Consumption", labelFont));
            billingTable.AddCell(CreateCell(billing.Reading.Consumption.ToString("N0"), valueFont));
            billingTable.AddCell(CreateCell("Cons. Amount", labelFont));
            billingTable.AddCell(CreateCell("Php " + billing.BaseAmount.ToString("N2"), valueFont));

            billingTable.AddCell(CreateCell("Arrears", labelFont));
            billingTable.AddCell(CreateCell("Php " + billing.Arrears.ToString("N2"), valueFont));
            billingTable.AddCell(CreateCell($"VAT({(billing.VATRate * 100)}%)", labelFont));
            billingTable.AddCell(CreateCell("Php " + billing.VATAmount.ToString("N2"), valueFont));

            var currentUser = User.FindFirstValue(ClaimTypes.Name) ?? "System";
            var reader = await _context.Users.FindAsync(billing.Reading.UserID);
            string readerName = reader?.FullName ?? currentUser;

            billingTable.AddCell(CreateCell($"Meter Reader: {readerName}", labelFont, 4));
            PdfPCell totalDueCell = CreateCell("Total Amount Due: Php " + billing.DueAmount.ToString("N2"), subtitleFont, 4);
            totalDueCell.HorizontalAlignment = Element.ALIGN_RIGHT;
            billingTable.AddCell(totalDueCell);

            document.Add(billingTable);
            document.Add(new Paragraph("\n"));

            // Create Overdue Details Table
            PdfPTable overdueTable = new PdfPTable(4)
            {
                WidthPercentage = 100
            };
            float[] overduewidths = new float[] { 20f, 30f, 20f, 30f };
            overdueTable.SetWidths(overduewidths);
            PdfPCell overdueHeaderCell = CreateCell("Overdue Details", subtitleFont, 4);
            overdueHeaderCell.BackgroundColor = lightBlue;
            overdueTable.AddCell(overdueHeaderCell);

            overdueTable.AddCell(CreateCell($"Penalty({billing.PenaltyRate * 100}%)", labelFont));
            overdueTable.AddCell(CreateCell("Php " + billing.Penalty.ToString("N2"), valueFont));
            overdueTable.AddCell(CreateCell("Over Due Amount", labelFont));
            overdueTable.AddCell(CreateCell("Php " + billing.OverDueAmount.ToString("N2"), subtitleFont));
            overdueTable.AddCell(CreateCell("Disconnection Date: " + billing.DisconnectionDate?.ToString("MMMM dd, yyyy"), valueFont, 4));

            document.Add(overdueTable);
            document.Add(new Paragraph("\n"));

            // Create Overdue Details Table
            PdfPTable rates = new PdfPTable(4)
            {
                WidthPercentage = 100
            };
            rates.SetWidths(overduewidths);
            PdfPCell ratesHeader = CreateCell("Billing Rates Details", valueFont, 4);
            ratesHeader.BackgroundColor = lightBlue;
            rates.AddCell(ratesHeader);

            rates.AddCell(CreateCell($"Rate Per Cubic Meter: Php {billing.MinimumCharge:N2}", smallFont, 4));
            rates.AddCell(CreateCell($"Minimum Consumption: {billing.MinimumConsumption:N0}", smallFont, 4));
            rates.AddCell(CreateCell($"Basic Charge: Php {billing.MinimumCharge:N2}", smallFont, 4));

            document.Add(rates);
            document.Add(new Paragraph("\n"));

            if (previousBillings.Any())
            {
                PdfPTable historyTable = new PdfPTable(5)
                {
                    WidthPercentage = 100
                };
                historyTable.SetWidths(new float[] { 20f, 20f, 20f, 20f, 20f });

                PdfPCell historyHeader = CreateCell("Previous Billing Records", valueFont, 5);
                historyHeader.BackgroundColor = lightBlue;
                historyTable.AddCell(historyHeader);

                // Table Headers
                historyTable.AddCell(CreateCell("Billing No.", labelFont));
                historyTable.AddCell(CreateCell("Period", labelFont));
                historyTable.AddCell(CreateCell("Consumption", labelFont));
                historyTable.AddCell(CreateCell("Due Amount", labelFont));
                historyTable.AddCell(CreateCell("Status", labelFont));

                foreach (var item in previousBillings)
                {
                    historyTable.AddCell(CreateCell(item.BillingNumber, smallFont));
                    historyTable.AddCell(CreateCell(item.BillingDate.ToString("MMMM yyyy"), smallFont));
                    historyTable.AddCell(CreateCell(item.Reading?.Consumption.ToString("N0") ?? "-", smallFont));
                    historyTable.AddCell(CreateCell("Php " + item.DueAmount.ToString("N2"), smallFont));
                    historyTable.AddCell(CreateCell(item.BillingStatus.ToString(), smallFont));
                }

                document.Add(historyTable);
                document.Add(new Paragraph("\n"));
            }

            // Italic font
            var italicFont = FontFactory.GetFont(FontFactory.HELVETICA_OBLIQUE, 9);

            // Payment reminder
            var dueDateText = $"Please pay on or before {billing.DueDate:MMMM dd, yyyy} to avoid penalties.";
            document.Add(new Paragraph(dueDateText, italicFont) { SpacingBefore = 10f });

            // Disclaimer
            var disclaimerText = "This is a system-generated billing statement. Any alteration or unauthorized reproduction is strictly prohibited.";
            document.Add(new Paragraph(disclaimerText, italicFont) { SpacingBefore = 5f });
            var noteText = "For inquiries, please send us an email at sitiosubicwaterworks@gmail.com. Thank you!";
            document.Add(new Paragraph(noteText, italicFont) { SpacingBefore = 5f });

            var endText = "********** END OF BILLING STATEMENT **********";
            document.Add(new Paragraph(endText, italicFont)
            {
                Alignment = Element.ALIGN_CENTER,
                SpacingBefore = 10f
            });

            document.Close();

            byte[] pdfBytes = memoryStream.ToArray();
            return File(pdfBytes, "application/pdf");
        }
    }
}
