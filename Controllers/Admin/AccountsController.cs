using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SitioSubicIMS.Web.Data;
using SitioSubicIMS.Web.Models;
using SitioSubicIMS.Web.Services.Logging;
using SitioSubicIMS.Web.ViewModels;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SitioSubicIMS.Web.Controllers.Admin
{
    [Authorize(Roles = "Administrator")]
    public class AccountsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditLogger _auditLogger;

        public AccountsController(ApplicationDbContext context, IAuditLogger auditLogger)
        {
            _context = context;
            _auditLogger = auditLogger;
        }

        public async Task<IActionResult> Index()
        {
            var accounts = await _context.Accounts
                .Where(a => a.IsActive)
                .Include(a => a.Meter) // Include the related Meter entity
                .OrderBy(a => a.AccountName)
                .AsNoTracking()
                .ToListAsync();

            return View(accounts);
        }

        [HttpGet]
        public IActionResult Create()
        {
            // Get only meters that are not tied to any active account
            var assignedMeterIds = _context.Accounts
                .Where(a => a.IsActive && a.MeterID != null)
                .Select(a => a.MeterID)
                .ToList();

            var availableMeters = _context.Meters
                .Where(m => m.IsActive && !assignedMeterIds.Contains(m.MeterID))
                .ToList();

            ViewBag.MeterList = new SelectList(availableMeters, "MeterID", "MeterNumber");
            return View("AccountForm", new Account());
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var account = await _context.Accounts.FindAsync(id);
            if (account == null || !account.IsActive)
            {
                TempData["Error"] = "Account not found.";
                return RedirectToAction(nameof(Index));
            }

            // Get meters not tied to any other active account
            var assignedMeterIds = _context.Accounts
                .Where(a => a.IsActive && a.MeterID != null && a.AccountID != id)
                .Select(a => a.MeterID)
                .ToList();

            var availableMeters = _context.Meters
                .Where(m => m.IsActive && (!assignedMeterIds.Contains(m.MeterID) || m.MeterID == account.MeterID))
                .ToList();

            ViewBag.MeterList = new SelectList(availableMeters, "MeterID", "MeterNumber", account.MeterID);
            return View("AccountForm", account);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(Account account)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "There is a problem saving the record.";
                return View("AccountForm", account);
            }

            var currentUser = User.Identity?.Name ?? "System";

            try
            {
                if (account.AccountID == 0)
                {
                    account.AccountNumber = GenerateAccountNumber();
                    account.DateCreated = DateTime.Now;
                    account.IsActive = true;
                    account.CreatedBy = currentUser;

                    _context.Accounts.Add(account);
                    await _auditLogger.LogAsync("Account", $"Created new account {account.AccountNumber}", currentUser);
                }
                else
                {
                    var existing = await _context.Accounts.FindAsync(account.AccountID);
                    if (existing == null)
                    {
                        TempData["Error"] = "Account not found.";
                        return RedirectToAction(nameof(Index));
                    }

                    existing.AccountName = account.AccountName;
                    existing.Address = account.Address;
                    existing.ContactNumber = account.ContactNumber;
                    existing.DateUpdated = DateTime.Now;
                    existing.UpdatedBy = currentUser;

                    _context.Accounts.Update(existing);
                    await _auditLogger.LogAsync("Account", $"Updated account {existing.AccountNumber}", currentUser);
                }

                await _context.SaveChangesAsync();

                TempData["Message"] = "Account saved successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                await _auditLogger.LogAsync("Account", $"Error saving account: {ex.Message}", currentUser);
                TempData["Error"] = "An error occurred while saving the account.";
                Console.WriteLine(ex);
                return View("AccountForm", account);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmDelete(int id)
        {
            var currentUser = User.Identity?.Name ?? "System";

            try
            {
                var account = await _context.Accounts.FindAsync(id);
                if (account == null || !account.IsActive)
                {
                    TempData["Error"] = "Account not found.";
                    return RedirectToAction(nameof(Index));
                }

                account.IsActive = false;
                account.DateUpdated = DateTime.Now;
                account.UpdatedBy = currentUser;

                _context.Accounts.Update(account);
                await _context.SaveChangesAsync();

                await _auditLogger.LogAsync("Account", $"Soft deleted account {account.AccountNumber}", currentUser);
                TempData["Message"] = "Account deactivated successfully.";

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                await _auditLogger.LogAsync("Account", $"Error deactivating account: {ex.Message}", currentUser);
                TempData["Error"] = "An error occurred while deactivating the account.";
                Console.WriteLine(ex);
                return RedirectToAction(nameof(Index));
            }
        }
        private string GenerateAccountNumber()
        {
            string prefix = "A";
            string dateSegment = DateTime.Now.ToString("MMyy");

            // Count how many accounts exist for the current MMYY
            var currentMonthYear = DateTime.Now.ToString("MMyy");
            int countForMonth = _context.Accounts
                .Count(a => a.AccountNumber.StartsWith(prefix + currentMonthYear));

            int nextSequence = countForMonth + 1;

            string paddedNumber = nextSequence.ToString("D5"); // D5 means 5-digit zero-padded
            return $"{prefix}{dateSegment}{paddedNumber}";
        }

    }
}
