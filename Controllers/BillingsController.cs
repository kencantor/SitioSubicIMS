using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SitioSubicIMS.Web.Data;
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
                    TempData["Warning"] = "Unable to send SMS: Account not found.";
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
                    $" Prev: {prev}. Curr: {curr}. Cons: {consumption}. Due Amount: {dueAmount}. Due Date: {dueDate}. Please pay on time to avoid penalties. Thank you!";

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
                await _auditLogger.LogAsync("Billing", $"Error confirming billing ID {id}: {ex.Message}", currentUser);
                return RedirectToAction(nameof(Index));
            }
        }
    }
}
