// Models/ViewModels/SystemInfoViewModel.cs
using System;

namespace SitioSubicIMS.Web.Models.ViewModels
{
    public class SystemInfoViewModel
    {
        public string ApplicationName { get; set; }
        public string Version { get; set; }
        public string Description { get; set; }
        public DateTime DeploymentDate { get; set; }
        public string ServerTime { get; set; }
        public string Hostname { get; set; }
        public string IpAddress { get; set; }
        public string OperatingSystem { get; set; }

        public string Developer { get; set; }
        public string ContactEmail { get; set; }

        public string CurrentUser { get; set; }
        public string UserRole { get; set; }
        public string LoginTime { get; set; }
    }
}
