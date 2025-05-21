using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SitioSubicIMS.Web.Data;
using SitioSubicIMS.Web.Models;
using System.Linq;

namespace SitioSubicIMS.Web.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class AuditLogsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AuditLogsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            var logs = _context.AuditLogs
                .OrderByDescending(a => a.Timestamp)
                .ToList();

            return View(logs);
        }
    }
}
