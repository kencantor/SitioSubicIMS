using Microsoft.AspNetCore.Mvc;

namespace SitioSubicIMS.Web.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated && User.Claims.Any(c => c.Type == System.Security.Claims.ClaimTypes.Role))
            {
                return RedirectToAction("Index", "Dashboard");
            }

            return View();

        }
    }
}
