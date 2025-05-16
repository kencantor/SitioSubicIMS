using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SitioSubicIMS.Web.Data;
using SitioSubicIMS.Web.Models;
using SitioSubicIMS.Web.Models.ViewModels;
using SitioSubicIMS.Web.Services.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SitioSubicIMS.Web.Controllers.Admin
{
    [Authorize(Roles = "Administrator")]
    public class SMSAlertsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditLogger _auditLogger;

        public SMSAlertsController(ApplicationDbContext context, IAuditLogger auditLogger)
        {
            _context = context;
            _auditLogger = auditLogger;
        }

        public async Task<IActionResult> Index()
        {
            var alerts = await _context.SMSAlerts
                .OrderByDescending(a => a.DateCreated)
                .ToListAsync();

            var viewModel = new SMSAlertViewModel
            {
                Latest = alerts.FirstOrDefault(a => a.IsActive),
                Alerts = alerts
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(SMSAlertViewModel viewModel)
        {
            if (viewModel?.Latest == null || !ModelState.IsValid)
            {
                TempData["Error"] = "Invalid input.";
                await _auditLogger.LogAsync("SMS Alert", "Invalid input during update attempt.", User.Identity?.Name ?? "System");
                return RedirectToAction("Index");
            }

            var input = viewModel.Latest;
            var currentUser = User.Identity?.Name ?? "System";

            try
            {
                using var transaction = await _context.Database.BeginTransactionAsync();

                var latest = await _context.SMSAlerts
                    .Where(a => a.IsActive)
                    .OrderByDescending(a => a.DateCreated)
                    .FirstOrDefaultAsync();

                bool isChanged =
                    latest == null ||
                    latest.AllowSMSAlerts != input.AllowSMSAlerts ||
                    latest.AllowReadingAlerts != input.AllowReadingAlerts ||
                    latest.AllowBillingAlerts != input.AllowBillingAlerts ||
                    latest.AllowPaymentAlerts != input.AllowPaymentAlerts ||
                    latest.MessageHeader != input.MessageHeader ||
                    latest.TwilioAccountSID != input.TwilioAccountSID ||
                    latest.TwilioAuthToken != input.TwilioAuthToken ||
                    latest.TwilioFromPhoneNumber != input.TwilioFromPhoneNumber;

                if (!isChanged)
                {
                    TempData["Message"] = "No changes detected. SMS settings not updated.";
                    await _auditLogger.LogAsync("SMS Alert", "Update attempted but no changes detected.", currentUser);
                    return RedirectToAction("Index");
                }

                // Deactivate current active
                var activeAlerts = await _context.SMSAlerts.Where(a => a.IsActive).ToListAsync();
                foreach (var alert in activeAlerts)
                {
                    alert.IsActive = false;
                    _context.SMSAlerts.Update(alert);
                }

                // Create new record
                var newAlert = new SMSAlert
                {
                    AllowSMSAlerts = input.AllowSMSAlerts,
                    AllowReadingAlerts = input.AllowReadingAlerts,
                    AllowBillingAlerts = input.AllowBillingAlerts,
                    AllowPaymentAlerts = input.AllowPaymentAlerts,
                    MessageHeader = input.MessageHeader,
                    TwilioAccountSID = input.TwilioAccountSID,
                    TwilioAuthToken = input.TwilioAuthToken,
                    TwilioFromPhoneNumber = input.TwilioFromPhoneNumber,
                    CreatedBy = currentUser,
                    DateCreated = DateTime.Now,
                    UpdatedBy = currentUser,
                    DateUpdated = DateTime.Now,
                    IsActive = true
                };

                _context.SMSAlerts.Add(newAlert);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                await _auditLogger.LogAsync("SMS Alert", "Updated SMS alert settings.", currentUser);
                TempData["Message"] = "SMS alert settings updated successfully.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "An error occurred while updating SMS alert settings.";
                await _auditLogger.LogAsync("SMS Alert", $"Error updating SMS alert: {ex.Message}", currentUser);
                Console.WriteLine(ex);
            }

            return RedirectToAction("Index");
        }
    }
}
