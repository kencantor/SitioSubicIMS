using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SitioSubicIMS.Web.Data;
using SitioSubicIMS.Web.Models;
using SitioSubicIMS.Web.Models.ViewModels;
using SitioSubicIMS.Web.Services.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Twilio.Types;

namespace SitioSubicIMS.Web.Controllers
{
    [Authorize(Roles = "Administrator,Reader")]
    public class ReadingsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditLogger _auditLogger;
        private readonly ISmsService _smsService;

        public ReadingsController(ApplicationDbContext context, IAuditLogger auditLogger, ISmsService smsService)
        {
            _context = context;
            _auditLogger = auditLogger;
            _smsService = smsService;
        }

        // Helper method to populate ViewBag.Meters with SelectListItems (MeterNumber + AccountName)
        private void PopulateMetersViewBag()
        {
            var metersWithActiveAccounts = (from meter in _context.Meters
                                            join account in _context.Accounts
                                            on meter.MeterID equals account.MeterID
                                            where account.IsActive // filter only active accounts here
                                            select new
                                            {
                                                MeterID = meter.MeterID,
                                                DisplayName = meter.MeterNumber + " - " + account.AccountName
                                            })
                              .Distinct() // optional, if meter-account pairs might be duplicated
                              .ToList();

            ViewBag.Meters = metersWithActiveAccounts.Select(m => new SelectListItem
            {
                Value = m.MeterID.ToString(),
                Text = m.DisplayName
            }).OrderBy(m => m.Text).ToList();
        }

        // GET: List all active readings
        public async Task<IActionResult> Index()
        {
            // Fetch readings with meters and accounts first
            var readingsData = await (from r in _context.Readings
                                      join m in _context.Meters on r.MeterID equals m.MeterID
                                      join a in _context.Accounts on m.MeterID equals a.MeterID into accGroup
                                      from a in accGroup.DefaultIfEmpty()
                                      where r.IsActive
                                      orderby r.ReadingDate descending
                                      select new
                                      {
                                          Reading = r,
                                          Meter = m,
                                          Account = a
                                      }).AsNoTracking().ToListAsync();

            // Fetch previous readings for each meter grouped by MeterID
            var previousReadings = _context.Readings
                .Where(r => r.IsActive)
                .GroupBy(r => r.MeterID)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.ReadingDate).ToList());

            var readingList = new List<ReadingListItemViewModel>();

            foreach (var item in readingsData)
            {
                var reading = item.Reading;
                var meter = item.Meter;
                var account = item.Account;

                // Find the previous reading before this reading's date
                decimal previousReadingValue = meter.FirstValue;
                if (previousReadings.TryGetValue(meter.MeterID, out var readingsForMeter))
                {
                    var previous = readingsForMeter
                        .FirstOrDefault(r => r.ReadingDate < reading.ReadingDate);
                    if (previous != null)
                    {
                        previousReadingValue = previous.ReadingValue;
                    }
                }

                var consumption = reading.ReadingValue - previousReadingValue;

                readingList.Add(new ReadingListItemViewModel
                {
                    ReadingId = reading.ReadingID,
                    AccountNumber = account.AccountName + " - " + account?.AccountNumber ?? "N/A",
                    MeterNumber = meter.MeterNumber,
                    ReadingDate = reading.ReadingDate,
                    PreviousReading = previousReadingValue,
                    CurrentReading = reading.ReadingValue,
                    Consumption = consumption,
                    Status = reading.IsActive ? "Active" : "Inactive",
                    DateCreated = reading.DateCreated,
                    IsActive = reading.IsActive,
                    BillingMonth = reading.BillingMonth,
                    BillingYear = reading.BillingYear,
                    IsBilled = reading.IsBilled
                });
                readingList = readingList
                    .OrderByDescending(r => r.BillingYear)
                    .ThenByDescending(r => r.BillingMonth)
                    .ThenBy(r => r.AccountNumber)
                    .ToList();
            }

            return View(readingList);
        }

        // GET: Create form
        [HttpGet]
        public IActionResult Create()
        {
            PopulateMetersViewBag();

            return View("ReadingForm", new Reading());
        }

        // GET: Edit form
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var reading = await _context.Readings.FindAsync(id);
            if (reading == null || !reading.IsActive)
            {
                TempData["Error"] = "Reading not found.";
                return RedirectToAction(nameof(Index));
            }

            PopulateMetersViewBag();
            //PopulateUsersViewBag();

            return View("ReadingForm", reading);
        }

        // POST: Save (Create/Edit)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(Reading reading)
        {
            if (!ModelState.IsValid)
            {
                PopulateMetersViewBag();
                return View("ReadingForm", reading);
            }

            string errorMessage;
            if (!IsValid(reading, out errorMessage))
            {
                PopulateMetersViewBag();
                TempData["Error"] = errorMessage;
                return View("ReadingForm", reading);
            }

            var currentUser = User.Identity?.Name ?? "System";

            try
            {
                var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                // Get the previous reading before the current reading date
                var previousReading = await _context.Readings
                    .Where(r => r.MeterID == reading.MeterID && r.ReadingDate < reading.ReadingDate)
                    .OrderByDescending(r => r.ReadingDate)
                    .FirstOrDefaultAsync();

                // Get the meter to retrieve its FirstValue if no previous reading exists
                var meter = await _context.Meters.FindAsync(reading.MeterID);
                decimal previousValue = previousReading?.ReadingValue ?? meter?.FirstValue ?? 0;

                // Assign to reading
                reading.PreviousReadingValue = previousValue;

                if (reading.ReadingID == 0)
                {
                    // New reading
                    reading.UserID = currentUserId;
                    reading.DateCreated = DateTime.Now;
                    reading.IsActive = true;
                    reading.CreatedBy = currentUser;

                    _context.Readings.Add(reading);
                    await _auditLogger.LogAsync("Reading", $"Created new reading for Meter # {meter?.MeterNumber}", currentUser);
                }
                else
                {
                    // Update existing
                    var existing = await _context.Readings.FindAsync(reading.ReadingID);
                    if (existing == null)
                    {
                        TempData["Error"] = "Reading not found.";
                        return RedirectToAction(nameof(Index));
                    }

                    existing.MeterID = reading.MeterID;
                    existing.UserID = reading.UserID;
                    existing.ReadingDate = reading.ReadingDate;
                    existing.ReadingValue = reading.ReadingValue;
                    existing.PreviousReadingValue = previousValue;
                    existing.BillingMonth = reading.BillingMonth;
                    existing.BillingYear = reading.BillingYear;
                    existing.IsBilled = reading.IsBilled;
                    existing.DateUpdated = DateTime.Now;
                    existing.UpdatedBy = currentUser;

                    _context.Readings.Update(existing);
                    await _auditLogger.LogAsync("Reading", $"Updated reading ID {existing.ReadingID}", currentUser);
                }

                await _context.SaveChangesAsync();

                await CreateOrUpdateBillingAsync(reading);

                TempData["Message"] = "Reading saved successfully.";

                // SMS ALERT
                var config = await _context.SMSAlerts
                    .Where(c => c.IsActive)
                    .OrderByDescending(c => c.DateCreated)
                    .FirstOrDefaultAsync();

                if (config != null && config.AllowSMSAlerts && config.AllowReadingAlerts)
                {
                    var account = await _context.Accounts
                        .Where(a => a.MeterID == reading.MeterID)
                        .FirstOrDefaultAsync();

                    var sysconfig = await _context.Configurations
                        .Where(s => s.IsActive)
                        .FirstOrDefaultAsync();

                    if (account != null && sysconfig != null)
                    {
                        decimal consumption = reading.ReadingValue - previousValue;

                        decimal charge = (consumption > sysconfig.MinimumConsumption)
                            ? consumption * sysconfig.PricePerCubicMeter
                            : sysconfig.MinimumCharge;

                        string phoneNumber = account.ContactNumber;
                        if (!string.IsNullOrWhiteSpace(phoneNumber))
                        {
                            var currentReader = await _context.Users.FindAsync(currentUserId);
                            string readerName = currentReader?.FullName ?? User.Identity.Name ?? "N/A";

                            string formattedCharge = "Php " + charge.ToString("N2");
                            string message = $"Dear Consumer, here is your reading details: Reading Value - {reading.ReadingValue}. " +
                                             $"Consumption: {consumption}. Due: {formattedCharge}. Thanks, Field Reader: {readerName}. ";

                            bool smsSent = await _smsService.SendSmsAsync(phoneNumber, message, currentUser);

                            if (smsSent)
                            {
                                await _auditLogger.LogAsync("SMS", $"SMS alert sent successfully to {phoneNumber} for meter ID {reading.MeterID}", currentUser);
                            }
                            else
                            {
                                await _auditLogger.LogAsync("SMS", $"Failed to send SMS alert to {phoneNumber} for meter ID {reading.MeterID}", currentUser);
                                TempData["Warning"] = "SMS Alert failed to send.";
                            }
                        }
                        else
                        {
                            await _auditLogger.LogAsync("SMS", $"No contact number found for account linked to meter ID {reading.MeterID}", currentUser);
                        }
                    }
                    else
                    {
                        await _auditLogger.LogAsync("SMS", $"No account or active system config found for meter ID {reading.MeterID} when sending SMS alert.", currentUser);
                    }
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                await _auditLogger.LogAsync("Reading", $"Error saving reading: {ex.Message}", currentUser);
                TempData["Error"] = "An error occurred while saving the reading.";
                Console.WriteLine(ex);

                PopulateMetersViewBag();
                return View("ReadingForm", reading);
            }
        }


        // POST: Soft delete reading
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmDelete(int id)
        {
            var currentUser = User.Identity?.Name ?? "System";

            try
            {
                var reading = await _context.Readings.FindAsync(id);
                if (reading == null || !reading.IsActive)
                {
                    TempData["Error"] = "Reading not found.";
                    return RedirectToAction(nameof(Index));
                }

                reading.IsActive = false;
                reading.DateUpdated = DateTime.Now;
                reading.UpdatedBy = currentUser;

                _context.Readings.Update(reading);
                await _context.SaveChangesAsync();

                await _auditLogger.LogAsync("Reading", $"Soft deleted reading ID {reading.ReadingID}", currentUser);
                TempData["Message"] = "Reading deleted successfully.";
            }
            catch (Exception ex)
            {
                await _auditLogger.LogAsync("Reading", $"Error deleting reading: {ex.Message}", currentUser);
                TempData["Error"] = "An error occurred while deleting the reading.";
                Console.WriteLine(ex);
            }

            return RedirectToAction(nameof(Index));
        }
        private bool IsValid(Reading reading, out string errorMessage)
        {
            // 1. Duplicate Reading Check
            bool exists = _context.Readings.Any(r =>
                r.IsActive &&
                r.MeterID == reading.MeterID &&
                r.BillingMonth == reading.BillingMonth &&
                r.BillingYear == reading.BillingYear &&
                r.ReadingID != reading.ReadingID
            );

            if (exists)
            {
                errorMessage = "A reading for this meter already exists for the selected billing month and year.";
                return false;
            }

            // 2. Get latest existing reading for this meter
            var latestReading = _context.Readings
                .Where(r => r.IsActive && r.MeterID == reading.MeterID)
                .OrderByDescending(r => r.BillingYear)
                .ThenByDescending(r => r.BillingMonth)
                .FirstOrDefault();

            if (latestReading != null)
            {
                // Prevent saving if new reading's billing period is older than the latest
                bool isEarlier = reading.BillingYear < latestReading.BillingYear ||
                                (reading.BillingYear == latestReading.BillingYear && reading.BillingMonth < latestReading.BillingMonth);

                if (isEarlier)
                {
                    errorMessage = $"Cannot save reading. The billing period ({reading.BillingMonth}/{reading.BillingYear}) is earlier than the latest existing record ({latestReading.BillingMonth}/{latestReading.BillingYear}).";
                    return false;
                }
            }

            // 3. Get previous reading value (to compare ReadingValue)
            Reading previousReading = _context.Readings
                .Where(r => r.IsActive &&
                            r.MeterID == reading.MeterID &&
                            (r.BillingYear < reading.BillingYear ||
                             (r.BillingYear == reading.BillingYear && r.BillingMonth < reading.BillingMonth)))
                .OrderByDescending(r => r.BillingYear)
                .ThenByDescending(r => r.BillingMonth)
                .FirstOrDefault();

            decimal previousValue;

            if (previousReading != null)
            {
                previousValue = previousReading.ReadingValue;
            }
            else
            {
                var meter = _context.Meters.FirstOrDefault(m => m.MeterID == reading.MeterID && m.IsActive);
                if (meter == null)
                {
                    errorMessage = "Associated meter not found or is inactive.";
                    return false;
                }

                previousValue = meter.FirstValue;
            }

            // 4. Reading Value should be greater than previous
            if (reading.ReadingValue <= previousValue)
            {
                errorMessage = $"The current reading ({reading.ReadingValue}) must be greater than the previous value ({previousValue}).";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        private async Task<string> GenerateBillingNumberAsync()
        {
            // Format: B + MM + yy + 5-digit zero-padded sequence (e.g. B05252400001)
            var now = DateTime.Now;
            string prefix = "B" + now.ToString("MMyy");

            // Get the max existing billing number with this prefix
            var maxBillingNumber = await _context.Billings
                .Where(b => b.BillingNumber.StartsWith(prefix))
                .OrderByDescending(b => b.BillingNumber)
                .Select(b => b.BillingNumber)
                .FirstOrDefaultAsync();

            int nextSequence = 1;
            if (maxBillingNumber != null)
            {
                // Extract the numeric suffix and increment
                string numericPart = maxBillingNumber.Substring(prefix.Length);
                if (int.TryParse(numericPart, out int lastSeq))
                {
                    nextSequence = lastSeq + 1;
                }
            }

            return prefix + nextSequence.ToString("D5"); // zero pad 5 digits
        }
        private async Task CreateOrUpdateBillingAsync(Reading reading)
        {
            var currentUser = User.Identity?.Name ?? "System";

            // Get active system configuration (assuming only one active)
            var config = await _context.Configurations
                .Where(c => c.IsActive)
                .FirstOrDefaultAsync();

            if (config == null)
            {
                throw new InvalidOperationException("Active system configuration not found.");
            }

            // Fetch all unpaid billings for the meter (excluding current reading)
            var unpaidBillings = await _context.Billings
                .Where(b => b.Reading.MeterID == reading.MeterID &&
                            b.BillingStatus == BillingStatus.Unpaid &&
                            b.IsActive &&
                            b.ReadingID != reading.ReadingID)
                .OrderByDescending(b => b.BillingDate)
                .ToListAsync();

            // Calculate arrears
            var previousArrears = unpaidBillings.Sum(b => b.OverDueAmount);

            // Mark latest unpaid billing as Overdue
            var latestUnpaid = unpaidBillings.FirstOrDefault();
            if (latestUnpaid != null)
            {
                latestUnpaid.BillingStatus = BillingStatus.Overdue;
                latestUnpaid.DateUpdated = DateTime.Now;
                latestUnpaid.UpdatedBy = currentUser;

                _context.Billings.Update(latestUnpaid);
                await _auditLogger.LogAsync("Billing", $"Marked Billing {latestUnpaid.BillingNumber} as Overdue", currentUser);
            }

            // Try to find existing billing for this reading
            var existingBilling = await _context.Billings
                .FirstOrDefaultAsync(b => b.ReadingID == reading.ReadingID);

            if (existingBilling == null)
            {
                // Create new billing record
                var billing = new Billing
                {
                    BillingNumber = await GenerateBillingNumberAsync(),
                    ReadingID = reading.ReadingID,
                    BillingDate = DateTime.Now,
                    RatePerCubicMeter = config.PricePerCubicMeter,
                    MinimumConsumption = config.MinimumConsumption,
                    MinimumCharge = config.MinimumCharge,
                    PenaltyRate = config.PenaltyRate,
                    VATRate = config.VATRate,
                    DueDate = DateTime.Now.AddDays(7),
                    DisconnectionDate = DateTime.Now.AddDays(15),
                    BillingStatus = BillingStatus.Pending,
                    DateCreated = DateTime.Now,
                    CreatedBy = currentUser,
                    IsActive = true,
                    Arrears = previousArrears
                };

                _context.Billings.Add(billing);
                await _auditLogger.LogAsync("Billing", $"Added Billing {billing.BillingNumber}", currentUser);
            }
            else
            {
                // Update existing billing record
                existingBilling.RatePerCubicMeter = config.PricePerCubicMeter;
                existingBilling.MinimumConsumption = config.MinimumConsumption;
                existingBilling.MinimumCharge = config.MinimumCharge;
                existingBilling.PenaltyRate = config.PenaltyRate;
                existingBilling.VATRate = config.VATRate;
                existingBilling.DueDate = DateTime.Now.AddDays(7);
                existingBilling.DisconnectionDate = DateTime.Now.AddDays(15);
                existingBilling.BillingStatus = BillingStatus.Pending;
                existingBilling.Arrears = previousArrears;
                existingBilling.DateUpdated = DateTime.Now;
                existingBilling.UpdatedBy = currentUser;

                _context.Billings.Update(existingBilling);
                await _auditLogger.LogAsync("Billing", $"Updated Billing {existingBilling.BillingNumber}", currentUser);
            }

            await _context.SaveChangesAsync();
        }
    }
}
