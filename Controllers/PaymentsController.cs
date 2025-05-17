using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SitioSubicIMS.Web.Data;
using SitioSubicIMS.Web.Models;
using SitioSubicIMS.Web.Models.ViewModels;
using SitioSubicIMS.Web.Services.Logging;
using System.Linq;
using System.Threading.Tasks;

namespace SitioSubicIMS.Web.Controllers
{
    [Authorize(Roles = "Administrator,Teller")]
    public class PaymentsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditLogger _auditLogger;
        private readonly ISmsService _smsService;

        public PaymentsController(ApplicationDbContext context, IAuditLogger auditLogger, ISmsService smsService)
        {
            _context = context;
            _auditLogger = auditLogger;
            _smsService = smsService;
        }

        public async Task<IActionResult> Index()
        {
            var payments = await _context.Payments
                .Include(p => p.Billing)
                    .ThenInclude(b => b.Reading)
                        .ThenInclude(r => r.Meter)
                .OrderByDescending(p => p.PaymentDate)
                .Select(p => new PaymentViewModel
                {
                    Payment = p,
                    Account = _context.Accounts.FirstOrDefault(a => a.IsActive && a.MeterID == p.Billing.Reading.MeterID)
                })
                .AsNoTracking()
                .ToListAsync();

            return View(payments);
        }

        [HttpPost]
        public async Task<IActionResult> Confirm(int id)
        {
            var currentUser = User.Identity?.Name ?? "System";

            try
            {
                var payment = await _context.Payments
                    .Include(p => p.Billing)
                        .ThenInclude(b => b.Payments)
                    .Include(p => p.Billing)
                        .ThenInclude(b => b.Reading)
                            .ThenInclude(r => r.Meter)
                    .FirstOrDefaultAsync(p => p.PaymentID == id);

                if (payment == null)
                    return NotFound();

                // Get configuration values
                var config = await _context.Configurations.FirstOrDefaultAsync();
                decimal minimumCharge = config?.MinimumCharge ?? 0m;
                decimal ratePerCubic = config?.PricePerCubicMeter ?? 20m;
                decimal vatRate = config?.VATRate ?? 0m;

                // Compute total amount due for the billing
                var billing = payment.Billing;
                decimal totalAmountDue;
                if (billing.Reading != null)
                {
                    decimal consumption = billing.Reading.Consumption;
                    decimal rawAmount = Math.Max(consumption * ratePerCubic, minimumCharge);
                    decimal vat = rawAmount * vatRate;
                    totalAmountDue = rawAmount + vat;
                }
                else
                {
                    decimal vat = minimumCharge * vatRate;
                    totalAmountDue = minimumCharge + vat;
                }

                // Sum existing posted payments (excluding this one if already posted)
                decimal existingPayments = billing.Payments
                    .Where(p => p.PaymentStatus == PaymentStatus.Posted && p.PaymentID != payment.PaymentID)
                    .Sum(p => p.AmountPaid);

                decimal totalAfterThisConfirmation = existingPayments + payment.AmountPaid;

                // Update payment
                payment.DateUpdated = DateTime.Now;
                payment.UpdatedBy = currentUser;
                payment.PaymentStatus = PaymentStatus.Posted;

                // Update billing status based on total paid
                if (totalAfterThisConfirmation >= totalAmountDue)
                {
                    billing.BillingStatus = BillingStatus.Paid;
                }
                else
                {
                    billing.BillingStatus = BillingStatus.Unpaid;
                }

                _context.Update(payment);
                await _context.SaveChangesAsync();

                await _auditLogger.LogAsync("Payment", $"Updated Payment # {payment.PaymentNumber}", currentUser);
                TempData["Message"] = "Payment confirmed.";

                await SendSmsAlertIfEnabled(payment, currentUser);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "An error occurred while confirming the payment.";
                await _auditLogger.LogAsync("Payment", $"Error confirming payment: {ex.Message}", currentUser);
            }

            return RedirectToAction(nameof(Index));
        }
        public async Task<IActionResult> PaymentForm(int? billingId)
        {
            var unpaidBillings = await _context.Billings
                .Where(b => b.BillingStatus == BillingStatus.Unpaid)
                .Include(b => b.Reading).ThenInclude(r => r.Meter)
                .Include(b => b.Payments)
                .OrderBy(b => b.DueDate)
                .ToListAsync();

            var meterIds = unpaidBillings.Select(b => b.Reading.MeterID).Distinct().ToList();

            var accounts = await _context.Accounts
                .Where(a => meterIds.Contains(a.MeterID))
                .ToListAsync();

            var unpaidBillingWithAccounts = unpaidBillings
                .Select(b => new UnpaidBillingInfo
                {
                    Billing = b,
                    Account = accounts.FirstOrDefault(a => a.MeterID == b.Reading.MeterID)
                }).ToList();

            Billing selectedBilling = null;
            Account selectedAccount = null;
            decimal existingPaymentsTotal = 0;
            decimal balance = 0;

            if (billingId.HasValue)
            {
                selectedBilling = unpaidBillings.FirstOrDefault(b => b.BillingID == billingId.Value);

                if (selectedBilling != null)
                {
                    selectedAccount = accounts.FirstOrDefault(a => a.MeterID == selectedBilling.Reading.MeterID);
                    existingPaymentsTotal = selectedBilling.Payments?.Sum(p => p.AmountPaid) ?? 0;
                    balance = selectedBilling.DueAmount - existingPaymentsTotal;
                }
            }

            return View(new PaymentFormViewModel
            {
                UnpaidBillings = unpaidBillingWithAccounts,
                SelectedBilling = selectedBilling,
                SelectedAccount = selectedAccount,
                ExistingPaymentsTotal = existingPaymentsTotal,
                Balance = balance,
                NewPayment = new Payment
                {
                    BillingID = billingId ?? 0,
                    PaymentStatus = PaymentStatus.Unposted,
                    PaymentDate = DateTime.Now
                }
            });
        }

        [HttpPost]
        public async Task<IActionResult> SubmitPayment(PaymentFormViewModel model)
        {
            var payment = model.NewPayment;

            var billing = await _context.Billings
                .Include(b => b.Payments)
                .Include(b => b.Reading)
                    .ThenInclude(r => r.Meter)
                .FirstOrDefaultAsync(b => b.BillingID == payment.BillingID);

            if (billing == null)
            {
                return NotFound();
            }

            // Get MinimumCharge and possibly other rates from Configurations
            var config = await _context.Configurations.FirstOrDefaultAsync();
            decimal minimumCharge = config?.MinimumCharge ?? 0m;
            decimal ratePerCubic = config?.PricePerCubicMeter ?? 20m;  // Optional, default to 10
            decimal vatRate = config?.VATRate ?? 0m;          // Optional, default to 12%

            // Calculate total due
            decimal totalAmountDue;
            if (billing.Reading != null)
            {
                decimal consumption = billing.Reading.Consumption;
                decimal rawAmount = Math.Max(consumption * ratePerCubic, minimumCharge);
                decimal vat = rawAmount * vatRate;
                totalAmountDue = rawAmount + vat;
            }
            else
            {
                // Fallback when no reading is available
                decimal vat = minimumCharge * vatRate;
                totalAmountDue = minimumCharge + vat;
            }

            // Calculate total payments including this one
            decimal existingPayments = billing.Payments
                .Where(p => p.PaymentStatus == PaymentStatus.Posted)
                .Sum(p => p.AmountPaid);

            decimal totalAfterThisPayment = existingPayments + payment.AmountPaid;

            // Set payment status
            if (payment.PaymentMethod == PaymentMethod.Cash || payment.PaymentMethod == PaymentMethod.Online)
            {
                payment.PaymentStatus = PaymentStatus.Posted;

                if (totalAfterThisPayment >= totalAmountDue)
                {
                    billing.BillingStatus = BillingStatus.Paid;
                }
                else
                {
                    billing.BillingStatus = BillingStatus.Unpaid;
                }
            }
            else if (payment.PaymentMethod == PaymentMethod.Check)
            {
                payment.PaymentStatus = PaymentStatus.Unposted;
                billing.BillingStatus = BillingStatus.Unpaid;
            }

            // Set other payment fields
            payment.PaymentDate = DateTime.Now;
            payment.DateCreated = DateTime.Now;
            payment.CreatedBy = User.Identity?.Name ?? "self";
            payment.PaymentNumber = await GeneratePaymentNumberAsync();

            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            await _auditLogger.LogAsync("Payments", $"Payment saved successfully. Payment # {payment.PaymentNumber}", User.Identity?.Name ?? "System");
            TempData["Success"] = "Payment added successfully.";

            return RedirectToAction("PaymentForm", new { billingId = payment.BillingID });
        }


        private async Task<string> GeneratePaymentNumberAsync()
        {
            var now = DateTime.Now;
            string prefix = "P" + now.ToString("MMyy");

            var lastNumber = await _context.Payments
                .Where(p => p.PaymentNumber.StartsWith(prefix))
                .OrderByDescending(p => p.PaymentNumber)
                .Select(p => p.PaymentNumber)
                .FirstOrDefaultAsync();

            int nextSeq = 1;
            if (!string.IsNullOrWhiteSpace(lastNumber))
            {
                string numericPart = lastNumber.Substring(prefix.Length);
                if (int.TryParse(numericPart, out int lastSeq))
                    nextSeq = lastSeq + 1;
            }

            return prefix + nextSeq.ToString("D5");
        }

        private async Task SendSmsAlertIfEnabled(Payment payment, string currentUser)
        {
            var config = await _context.SMSAlerts
                .Where(c => c.IsActive)
                .OrderByDescending(c => c.DateCreated)
                .FirstOrDefaultAsync();

            if (config == null || !config.AllowSMSAlerts || !config.AllowPaymentAlerts)
                return;

            var sysConfig = await _context.Configurations.FirstOrDefaultAsync(c => c.IsActive);
            var reading = payment.Billing?.Reading;
            if (reading == null || sysConfig == null)
                return;

            string period = new DateTime(reading.BillingYear, reading.BillingMonth, 1).ToString("MMMM yyyy");
            var account = await _context.Accounts.FirstOrDefaultAsync(a => a.MeterID == reading.MeterID);

            if (account == null || string.IsNullOrWhiteSpace(account.ContactNumber))
            {
                await _auditLogger.LogAsync("SMS", $"No contact number for account in Payment # {payment.PaymentNumber}", currentUser);
                return;
            }

            string readerName = (await _context.Users.FindAsync(reading.UserID))?.FullName ?? currentUser;
            string message = $"Dear Consumer, your payment for {period} has been received. Amount Paid: Php {payment.AmountPaid:N2}. Thank you!";

            bool smsSent = await _smsService.SendSmsAsync(account.ContactNumber, message, currentUser);

            if (smsSent)
                await _auditLogger.LogAsync("SMS", $"SMS sent to {account.ContactNumber} for Payment # {payment.PaymentNumber}", currentUser);
            else
                await _auditLogger.LogAsync("SMS", $"SMS failed to {account.ContactNumber} for Payment # {payment.PaymentNumber}", currentUser);
        }
    }
}
