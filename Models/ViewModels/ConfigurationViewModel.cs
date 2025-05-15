namespace SitioSubicIMS.Web.Models.ViewModels
{
    public class ConfigurationViewModel
    {
        public Configuration Latest { get; set; }

        // Initialize with empty list to avoid null reference issues
        public List<Configuration> Configurations { get; set; } = new List<Configuration>();
    }
}
