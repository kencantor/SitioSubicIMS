using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SitioSubicIMS.Web.Controllers
{
    // Use Authorize to restrict access to logged-in users
    [Authorize]
    public class DashboardController : Controller
    {
        public IActionResult Index()
        {
            // Controller logic here
            return View(); // Ensure the view exists in Views/Dashboard/Index.cshtml
        }
    }
}
