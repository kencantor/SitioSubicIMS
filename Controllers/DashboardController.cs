using Microsoft.AspNetCore.Mvc;

namespace SitioSubicIMS.Web.Controllers
{
    public class DashboardController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
