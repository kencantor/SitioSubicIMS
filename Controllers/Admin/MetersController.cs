using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    public class MetersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditLogger _auditLogger;

        public MetersController(ApplicationDbContext context, IAuditLogger auditLogger)
        {
            _context = context;
            _auditLogger = auditLogger;
        }

        // GET: List meters
        public async Task<IActionResult> Index()
        {
            var meters = await _context.Meters
                .Where(m => m.IsActive)
                .OrderByDescending(m => m.DateCreated)
                .AsNoTracking()
                .ToListAsync();

            return View(meters);
        }


        // GET: Show form for creating new meter
        [HttpGet]
        public IActionResult Create()
        {
            return View("MeterForm", new Meter());
        }

        // GET: Show form for editing meter by id
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var meter = await _context.Meters.FindAsync(id);
            if (meter == null || !meter.IsActive)
            {
                TempData["Error"] = "Meter not found.";
                return RedirectToAction(nameof(Index));
            }
            return View("MeterForm", meter);
        }

        // POST: Handle both Create and Edit form submission
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(Meter meter)
        {
            if (!ModelState.IsValid)
            {
                // Return form with validation errors
                return View("MeterForm", meter);
            }
            if (!IsValid(meter, out string errorMessage))
            {
                ModelState.AddModelError(nameof(meter.MeterNumber), errorMessage);
                return View("MeterForm", meter);
            }
            var currentUser = User.Identity?.Name ?? "System";

            try
            {
                if (meter.MeterID == 0)
                {
                    // New meter
                    meter.DateCreated = DateTime.Now;
                    meter.IsActive = true;
                    meter.CreatedBy = currentUser;

                    _context.Meters.Add(meter);
                    await _auditLogger.LogAsync("Meter", $"Created new meter {meter.MeterNumber}", currentUser);
                }
                else
                {
                    // Edit existing meter
                    var existing = await _context.Meters.FindAsync(meter.MeterID);
                    if (existing == null)
                    {
                        TempData["Error"] = "Meter not found.";
                        return RedirectToAction(nameof(Index));
                    }

                    existing.MeterNumber = meter.MeterNumber;
                    existing.SerialNumber = meter.SerialNumber;
                    existing.Make = meter.Make;
                    existing.FirstValue = meter.FirstValue;
                    existing.DateUpdated = DateTime.Now;
                    existing.UpdatedBy = currentUser;

                    _context.Meters.Update(existing);
                    await _auditLogger.LogAsync("Meter", $"Updated meter {existing.MeterNumber}", currentUser);
                }

                await _context.SaveChangesAsync();

                TempData["Message"] = "Meter saved successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                await _auditLogger.LogAsync("Meter", $"Error saving meter: {ex.Message}", currentUser);
                TempData["Error"] = "An error occurred while saving the meter.";
                Console.WriteLine(ex);
                return View("MeterForm", meter);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmDelete(int id)
        {
            var currentUser = User.Identity?.Name ?? "System";

            try
            {
                var meter = await _context.Meters.FindAsync(id);
                if (meter == null || !meter.IsActive)
                {
                    TempData["Error"] = "Meter not found.";
                    return RedirectToAction(nameof(Index));
                }

                // Check if the meter is assigned to any active account
                bool isMeterInUse = await _context.Accounts
                    .AnyAsync(a => a.MeterID == id && a.IsActive);

                if (isMeterInUse)
                {
                    TempData["Error"] = "Meter cannot be deleted because it is assigned to an active account.";
                    return RedirectToAction(nameof(Index));
                }

                // Proceed with soft delete
                meter.IsActive = false;
                meter.DateUpdated = DateTime.Now;
                meter.UpdatedBy = currentUser;

                _context.Meters.Update(meter);
                await _context.SaveChangesAsync();

                await _auditLogger.LogAsync("Meter", $"Soft deleted meter {meter.MeterNumber}", currentUser);
                TempData["Message"] = "Meter deactivated successfully.";

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                await _auditLogger.LogAsync("Meter", $"Error deactivating meter: {ex.Message}", currentUser);
                TempData["Error"] = "An error occurred while deactivating the meter.";
                Console.WriteLine(ex);
                return RedirectToAction(nameof(Index));
            }
        }

        private bool IsValid(Meter meter, out string errorMessage)
        {
            // Check for duplicate meter number
            bool isDuplicate = _context.Meters.Any(m =>
                m.IsActive &&
                m.MeterNumber == meter.MeterNumber &&
                m.MeterID != meter.MeterID
            );

            if (isDuplicate)
            {
                errorMessage = "Meter number already exists.";
                return false;
            }

            // You can add more custom validations here later

            errorMessage = string.Empty;
            return true;
        }

    }
}
