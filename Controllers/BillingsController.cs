using iTextSharp.text.pdf;
using iTextSharp.text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SitioSubicIMS.Web.Data;
using SitioSubicIMS.Web.Helpers;
using SitioSubicIMS.Web.Models;
using SitioSubicIMS.Web.Models.ViewModels;
using SitioSubicIMS.Web.Services.Logging;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SitioSubicIMS.Web.Controllers
{
    [Authorize(Roles = "Administrator,Teller")]
    public class BillingsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditLogger _auditLogger;
        private readonly ISmsService _smsService;

        public BillingsController(ApplicationDbContext context, IAuditLogger auditLogger, ISmsService smsService)
        {
            _context = context;
            _auditLogger = auditLogger;
            _smsService = smsService;
        }

        public async Task<IActionResult> Index()
        {
            // Load active billings with readings and meters
            var billings = await _context.Billings
                .Where(b => b.IsActive)
                .Include(b => b.Reading)
                    .ThenInclude(r => r.Meter)
                .AsNoTracking()
                .ToListAsync();

            // Load active accounts to avoid subqueries in projection
            var accounts = await _context.Accounts
                .Where(a => a.IsActive)
                .ToListAsync();

            // Map billings to view models with related accounts
            var billingViewModels = billings
                .Select(b => new BillingViewModel
                {
                    Billing = b,
                    Account = accounts.FirstOrDefault(a => a.MeterID == b.Reading.MeterID)
                })
                .OrderByDescending(b => b.Billing.BillingDate)
                .ToList();

            return View(billingViewModels);
        }

        [HttpPost]
        public async Task<IActionResult> Confirm(int id)
        {
            var currentUser = User.FindFirstValue(ClaimTypes.Name) ?? "System";

            try
            {
                // Load billing with related reading and meter
                var billing = await _context.Billings
                    .Include(b => b.Reading)
                    .ThenInclude(r => r.Meter)
                    .FirstOrDefaultAsync(b => b.BillingID == id);

                if (billing == null)
                    return NotFound();

                // Update billing status and properties
                billing.DateUpdated = DateTime.Now;
                billing.UpdatedBy = currentUser;
                billing.BillingStatus = BillingStatus.Unpaid;
                billing.Reading.IsBilled = true;

                _context.Update(billing);
                await _context.SaveChangesAsync();

                await _auditLogger.LogAsync("Billing", $"Updated Billing # {billing.BillingNumber}", currentUser);

                TempData["Message"] = "Billing confirmed.";

                // Proceed to send SMS alert if configured
                var config = await _context.SMSAlerts
                    .Where(c => c.IsActive)
                    .OrderByDescending(c => c.DateCreated)
                    .FirstOrDefaultAsync();

                if (config == null || !config.AllowSMSAlerts || !config.AllowBillingAlerts)
                {
                    return RedirectToAction(nameof(Index));
                }

                var sysconfig = await _context.Configurations
                    .Where(s => s.IsActive)
                    .FirstOrDefaultAsync();

                if (sysconfig == null)
                {
                    await _auditLogger.LogAsync("SMS", $"No active system configuration found when sending SMS alert.", currentUser);
                    return RedirectToAction(nameof(Index));
                }

                var meterId = billing.Reading.MeterID;
                var account = await _context.Accounts.FirstOrDefaultAsync(a => a.MeterID == meterId);

                if (account == null)
                {
                    TempData["Warning"] = "Unable to send SMS Account not found.";
                    await _auditLogger.LogAsync("SMS", $"No account found for Meter ID {meterId} when sending SMS alert.", currentUser);
                    return RedirectToAction(nameof(Index));
                }

                string phoneNumber = account.ContactNumber;
                if (string.IsNullOrWhiteSpace(phoneNumber))
                {
                    await _auditLogger.LogAsync("SMS", $"No contact number found for account linked to Billing # {billing.BillingNumber}", currentUser);
                    return RedirectToAction(nameof(Index));
                }

                // Prepare SMS message details
                int monthNumber = billing.Reading.BillingMonth;
                int year = billing.Reading.BillingYear;
                string period = new DateTime(year, monthNumber, 1).ToString("MMMM yyyy");
                string prev = billing.Reading.PreviousReadingValue.ToString("N0");
                string curr = billing.Reading.ReadingValue.ToString("N0");
                string consumption = billing.Reading.Consumption.ToString("N0");
                string dueAmount = "Php " + billing.DueAmount.ToString("N2");
                string dueDate = billing.DueDate.ToString("MMM dd, yyyy");

                var reader = await _context.Users.FindAsync(billing.Reading.UserID);
                string readerName = reader?.FullName ?? currentUser;

                string message = $"Dear Consumer, your Billing details for {period} are now available." +
                    $" Prev {prev}. Curr {curr}. Cons {consumption}. Due Amount {dueAmount}. Due Date {dueDate}. Please pay on time to avoid penalties. Thank you!  This is a system generated message. Do not reply.";

                bool smsSent = await _smsService.SendSmsAsync(phoneNumber, message, currentUser);

                if (smsSent)
                {
                    await _auditLogger.LogAsync("SMS", $"SMS alert sent successfully to {phoneNumber} for Billing # {billing.BillingNumber}", currentUser);
                }
                else
                {
                    await _auditLogger.LogAsync("SMS", $"Failed to send SMS alert to {phoneNumber} for Billing # {billing.BillingNumber}", currentUser);
                    TempData["Warning"] = "SMS Alert failed to send.";
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = "An error occurred while confirming the billing.";
                await _auditLogger.LogAsync("Billing", $"Error confirming billing ID {id} {ex.Message}", currentUser);
                return RedirectToAction(nameof(Index));
            }
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
                accountTable.AddCell(CreateCell(account?.Address ?? "-", valueFont, 3));
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
}
