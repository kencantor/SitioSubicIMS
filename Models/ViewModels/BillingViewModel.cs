namespace SitioSubicIMS.Web.Models.ViewModels
{
    public class BillingViewModel
    {
        public Billing Billing { get; set; }
        public Account Account { get; set; }
        public Meter Meter => Billing?.Reading?.Meter;
    }
}
