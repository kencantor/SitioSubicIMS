// Controllers/SystemInfoController.cs
using Microsoft.AspNetCore.Mvc;
using SitioSubicIMS.Web.Models.ViewModels;
using System.Net;

namespace SitioSubicIMS.Web.Controllers
{
    public class SystemInfoController : Controller
    {
        public IActionResult Index()
        {
            var user = HttpContext.User.Identity.Name ?? "Guest";
            var userRole = User.IsInRole("Administrator") ? "Administrator" : "Standard User"; // adjust as needed

            var model = new SystemInfoViewModel
            {
                ApplicationName = "Sitio Subic Waterworks Integrated Management System",
                Version = "v1.0.0",
                Description = "An integrated management system for handling billing, payments, consumers, SMS alerts and more.",
                DeploymentDate = new DateTime(2025, 5, 15),
                ServerTime = DateTime.Now.ToString("f"),
                Hostname = Dns.GetHostName(),
                IpAddress = Dns.GetHostAddresses(Dns.GetHostName()).FirstOrDefault(ip => ip.AddressFamily.ToString() == "InterNetwork")?.ToString() ?? "N/A",
                OperatingSystem = System.Runtime.InteropServices.RuntimeInformation.OSDescription,

                Developer = "Sitio Subic Waterworks Dev Team",
                ContactEmail = "sitiosubicwaterworks@gmail.com",

                CurrentUser = user,
                UserRole = userRole,
                LoginTime = DateTime.Now.ToString("f") // optional: replace with session login time if stored
            };

            return View(model);
        }
    }
}
