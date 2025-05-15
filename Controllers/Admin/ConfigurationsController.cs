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
    public class ConfigurationsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditLogger _auditLogger;

        public ConfigurationsController(ApplicationDbContext context, IAuditLogger auditLogger)
        {
            _context = context;
            _auditLogger = auditLogger;
        }

        public async Task<IActionResult> Index()
        {
            var configurations = await _context.Configurations
                .OrderByDescending(c => c.DateCreated)
                .ToListAsync();

            var viewModel = new ConfigurationViewModel
            {
                Latest = configurations.FirstOrDefault(c => c.IsActive),
                Configurations = configurations
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(ConfigurationViewModel viewModel)
        {
            if (viewModel?.Latest == null || !ModelState.IsValid)
            {
                TempData["Error"] = "Invalid input.";
                await _auditLogger.LogAsync("Configuration", "Invalid input during update attempt.", User.Identity?.Name ?? "System");
                return RedirectToAction("Index");
            }

            var input = viewModel.Latest;
            var currentUser = User.Identity?.Name ?? "System";

            try
            {
                using var transaction = await _context.Database.BeginTransactionAsync();

                var latest = await _context.Configurations
                    .Where(c => c.IsActive)
                    .OrderByDescending(c => c.DateCreated)
                    .FirstOrDefaultAsync();

                bool isChanged =
                    latest == null ||
                    latest.PricePerCubicMeter != input.PricePerCubicMeter ||
                    latest.MinimumConsumption != input.MinimumConsumption ||
                    latest.MinimumCharge != input.MinimumCharge ||
                    latest.PenaltyRate != input.PenaltyRate ||
                    latest.VATRate != input.VATRate;

                if (!isChanged)
                {
                    TempData["Message"] = "No changes detected. Configuration not updated.";
                    await _auditLogger.LogAsync("Configuration", "Update attempted but no changes detected.", currentUser);
                    return RedirectToAction("Index");
                }

                // Deactivate all active configs
                var activeConfigs = await _context.Configurations.Where(c => c.IsActive).ToListAsync();
                foreach (var config in activeConfigs)
                {
                    config.IsActive = false;
                    _context.Configurations.Update(config);
                }

                // Create new config
                var newConfig = new Configuration
                {
                    PricePerCubicMeter = input.PricePerCubicMeter,
                    MinimumConsumption = input.MinimumConsumption,
                    MinimumCharge = input.MinimumCharge,
                    PenaltyRate = input.PenaltyRate,
                    VATRate = input.VATRate,
                    CreatedBy = currentUser,
                    DateCreated = DateTime.Now,
                    IsActive = true
                };

                _context.Configurations.Add(newConfig);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                await _auditLogger.LogAsync("Configuration", "Updated configuration settings.", currentUser);
                TempData["Message"] = "Configuration updated successfully.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "An error occurred while updating configuration.";
                await _auditLogger.LogAsync("Configuration", $"Error updating configuration: {ex.Message}", currentUser);
                Console.WriteLine(ex);
            }

            return RedirectToAction("Index");
        }
    }
}
