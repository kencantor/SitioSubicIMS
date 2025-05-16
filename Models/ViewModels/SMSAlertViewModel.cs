namespace SitioSubicIMS.Web.Models.ViewModels
{
    public class SMSAlertViewModel
    {
        public SMSAlert Latest { get; set; }
        public List<SMSAlert> Alerts { get; set; } = new List<SMSAlert>();
    }

}
