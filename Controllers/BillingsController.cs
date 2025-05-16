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
            var billings = await _context.Billings
                .Where(b => b.IsActive)
                .Include(b => b.Reading)
                    .ThenInclude(r => r.Meter)
                .Select(b => new BillingViewModel
                {
                    Billing = b,
                    Account = _context.Accounts
                        .Where(a => a.IsActive && a.MeterID == b.Reading.MeterID)
                        .FirstOrDefault()
                })
                .OrderByDescending(b => b.Billing.BillingDate)
                .AsNoTracking()
                .ToListAsync();

            return View(billings);
        }

        [HttpPost]
        public async Task<IActionResult> Confirm(int id)
        {
            var currentUser = User.Identity?.Name ?? "System";
            try
            {
                var billing = await _context.Billings
                    .Include(b => b.Reading)
                    .ThenInclude(r => r.Meter)
                    .FirstOrDefaultAsync(b => b.BillingID == id);

                if (billing == null)
                {
                    return NotFound();
                }

                billing.DateUpdated = DateTime.Now;
                billing.UpdatedBy = currentUser;
                billing.BillingStatus = BillingStatus.Unpaid;
                billing.Reading.IsBilled = true;

                _context.Update(billing);
                await _context.SaveChangesAsync();
                await _auditLogger.LogAsync("Billing", $"Updated Billing # {billing.BillingNumber}", currentUser);
                TempData["Message"] = "Billing confirmed.";

                // SMS ALERT
                var config = await _context.SMSAlerts
                    .Where(c => c.IsActive)
                    .OrderByDescending(c => c.DateCreated)
                    .FirstOrDefaultAsync();

                if (config != null && config.AllowSMSAlerts && config.AllowBillingAlerts)
                {
                    var sysconfig = await _context.Configurations
                        .Where(s => s.IsActive)
                        .FirstOrDefaultAsync();

                    if (billing != null && sysconfig != null)
                    {
                        int monthNumber = billing.Reading.BillingMonth;  // e.g. 5
                        int year = billing.Reading.BillingYear;          // e.g. 2025
                        string period = new DateTime(year, monthNumber, 1).ToString("MMMM yyyy");
                        string prev = billing.Reading.PreviousReadingValue.ToString("N0");
                        string curr = billing.Reading.ReadingValue.ToString("N0");
                        string consumption = billing.Reading.Consumption.ToString("N0");
                        string dueamount = "Php " + billing.DueAmount.ToString("N2"); ;
                        string duedate = billing.DueDate.ToString("MMM dd, yyyy");
                        //string overdueamount = "Php " + billing.DueDate.ToString("MMM dd, yyyy");

                        var meterId = billing.Reading.MeterID;
                        var account = await _context.Accounts.FirstOrDefaultAsync(a => a.MeterID == meterId);

                        if (account == null)
                        {
                            TempData["Warning"] = "Unable to send SMS.";
                            return RedirectToAction(nameof(Index));
                        }

                        string phoneNumber = account.ContactNumber;
                        if (!string.IsNullOrWhiteSpace(phoneNumber))
                        {
                            var reader = await _context.Users.FindAsync(billing.Reading.UserID);
                            string readerName = reader?.FullName ?? User.Identity.Name ?? "N/A";

                            string message = $"Dear Consumer, your Billing details for {period} are now available." +
                                $" Prev: {prev}. Curr: {curr}. Cons: {consumption}. Due Amount: {dueamount}. Due Date: {duedate}. Please pay on time to avoid penalties. Thank you!";

                            bool smsSent = await _smsService.SendSmsAsync(phoneNumber, message, currentUser);

                            if (smsSent)
                            {
                                await _auditLogger.LogAsync("SMS", $"SMS alert sent successfully to {phoneNumber} for Billing # {billing.BillingNumber}", currentUser);
                            }
                            else
                            {
                                await _auditLogger.LogAsync("SMS", $"Failed to send SMS alert to {phoneNumber} for meter ID {billing.BillingNumber}", currentUser);
                                TempData["Warning"] = "SMS Alert failed to send.";
                            }
                        }
                        else
                        {
                            await _auditLogger.LogAsync("SMS", $"No contact number found for account linked to Billing # {billing.BillingNumber}", currentUser);
                        }
                    }
                    else
                    {
                        await _auditLogger.LogAsync("SMS", $"No account or active system config found when sending SMS alert.", currentUser);
                    }
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                // Optionally log the error
                TempData["Error"] = "An error occurred while confirming the billing.";
                // Log to audit or file if needed:
                await _auditLogger.LogAsync("Billing", $"An error occurred while confirming the billing. {ex.Message}", currentUser);
                return RedirectToAction(nameof(Index));
            }
        }


    }
}
