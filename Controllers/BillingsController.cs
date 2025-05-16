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

namespace SitioSubicIMS.Web.Controllers
{
    [Authorize(Roles = "Administrator,Teller")]
    public class BillingsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditLogger _auditLogger;

        public BillingsController(ApplicationDbContext context, IAuditLogger auditLogger)
        {
            _context = context;
            _auditLogger = auditLogger;
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
    }
}
