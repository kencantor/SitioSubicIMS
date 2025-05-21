using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SitioSubicIMS.Web.Data;
using SitioSubicIMS.Web.Models;
using SitioSubicIMS.Web.Models.ViewModels;

namespace SitioSubicIMS.Web.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReportsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var totalDueAmount = await _context.Billings
                .Include(p => p.Payments)
                .Include(b => b.Reading)
                .ThenInclude(r => r.Meter)
                .Where(b => b.BillingStatus == BillingStatus.Unpaid || b.BillingStatus == BillingStatus.Pending)
                .AsNoTracking()
                .ToListAsync();

            var sumDue = totalDueAmount
                .Sum(b => (b.DueDate > DateTime.Now ? b.DueAmount : b.OverDueAmount) - b.Payments.Sum(a => a.AmountPaid));

            var pastdues = totalDueAmount.Count(b => b.DueDate < DateTime.Now);

            var reportData = new ReportDashboardViewModel
            {
                TotalAccounts = await _context.Accounts.Where(a => a.IsActive).CountAsync(),
                TotalBillings = sumDue,
                UnpaidBillingsCount = totalDueAmount.Count,
                TotalPayments = await _context.Payments.SumAsync(p => p.AmountPaid),
                PastDues = pastdues
            };

            return View(reportData);
        }

        public async Task<IActionResult> GetActiveAccountsModal()
        {
            var activeAccounts = await _context.Accounts
                .Include(a => a.Meter)
                .Where(a => a.IsActive)
                .OrderBy(a => a.AccountName)
                .AsNoTracking()
                .ToListAsync();

            return PartialView("Partials/_ActiveAccountsModal", activeAccounts);
        }

        public async Task<IActionResult> GetUnpaidBillingsModal()
        {
            var unpaidBillings = await _context.Billings
                .Include(b => b.Payments)
                .Include(b => b.Reading)
                    .ThenInclude(r => r.Meter)
                .Where(b => b.BillingStatus == BillingStatus.Unpaid || b.BillingStatus == BillingStatus.Pending)
                .AsNoTracking()
                .ToListAsync();

            var accounts = await _context.Accounts
                .Where(a => a.IsActive)
                .AsNoTracking()
                .ToListAsync();

            var data = unpaidBillings.Select(b =>
            {
                var account = accounts.FirstOrDefault(a => a.MeterID == b.Reading.MeterID);

                return new UnpaidBillingViewModel
                {
                    AccountName = account?.AccountName ?? "[Unknown]",
                    BillingNumber = b.BillingNumber,
                    MeterNumber = b.Reading.Meter?.MeterNumber ?? "[N/A]",
                    Period = new DateTime(b.Reading.BillingYear, b.Reading.BillingMonth, 1).ToString("MMMM yyyy"),
                    DueAmount = b.DueDate > DateTime.Now ? b.DueAmount : b.OverDueAmount,
                    Paid = b.Payments.Sum(p => p.AmountPaid),
                    Balance = (b.DueDate > DateTime.Now ? b.DueAmount : b.OverDueAmount) - b.Payments.Sum(p => p.AmountPaid)
                };
            }).ToList();

            return PartialView("Partials/_UnpaidBillingsModal", data);
        }

        public async Task<IActionResult> GetPaymentsCollectedModal()
        {
            var payments = await _context.Payments
                .Include(p => p.Billing)
                    .ThenInclude(b => b.Reading)
                .AsNoTracking()
                .OrderByDescending(p => p.PaymentDate)
                .ToListAsync();

            var accounts = await _context.Accounts.AsNoTracking().ToListAsync();

            var data = payments.Select(p =>
            {
                var billing = p.Billing;
                var reading = billing?.Reading;
                var account = accounts.FirstOrDefault(a => a.MeterID == reading?.MeterID);

                return new PaymentsCollectedViewModel
                {
                    AccountName = account?.AccountName ?? "[Unknown]",
                    PaymentNumber = p?.PaymentNumber ?? "N/A",
                    BillingNumber = billing?.BillingNumber ?? "[N/A]",
                    PaymentDate = p.PaymentDate,
                    PaymentMethod = p.PaymentMethod.ToString(),
                    AmountPaid = p.AmountPaid
                };
            }).ToList();

            return PartialView("Partials/_PaymentsCollectedModal", data);
        }
        public async Task<IActionResult> GetPastDuesModal()
        {
            var pastDues = await _context.Billings
                .Include(b => b.Payments)
                .Include(b => b.Reading)
                    .ThenInclude(r => r.Meter)
                .Where(b =>
                    (b.BillingStatus == BillingStatus.Unpaid || b.BillingStatus == BillingStatus.Pending) &&
                    b.DueDate < DateTime.Now)
                .AsNoTracking()
                .ToListAsync();

            var accounts = await _context.Accounts
                .Where(a => a.IsActive)
                .AsNoTracking()
                .ToListAsync();

            var data = pastDues.Select(b =>
            {
                var account = accounts.FirstOrDefault(a => a.MeterID == b.Reading.MeterID);
                var paidAmount = b.Payments.Sum(p => p.AmountPaid);
                var balance = b.OverDueAmount - paidAmount;
                var dueDate = b.DueDate;
                var daysOverdue = (DateTime.Now - dueDate).Days;

                return new PastDueViewModel
                {
                    AccountName = account?.AccountName ?? "[Unknown]",
                    BillingNumber = b.BillingNumber,
                    MeterNumber = b.Reading.Meter?.MeterNumber ?? "[N/A]",
                    BillingPeriod = new DateTime(b.Reading.BillingYear, b.Reading.BillingMonth, 1).ToString("MMMM yyyy"),
                    DueDate = dueDate,
                    DaysOverdue = daysOverdue,
                    OverDueAmount = b.OverDueAmount,
                    Paid = paidAmount,
                    Balance = balance
                };
            }).ToList();

            return PartialView("Partials/_PastDuesModal", data);
        }
    }
}
