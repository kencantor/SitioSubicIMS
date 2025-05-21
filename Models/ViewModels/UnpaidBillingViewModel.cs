namespace SitioSubicIMS.Web.Models.ViewModels
{
    public class UnpaidBillingViewModel
    {
        public string AccountName { get; set; }
        public string BillingNumber { get; set; }
        public string MeterNumber { get; set; }
        public string Period { get; set; }
        public decimal DueAmount { get; set; }
        public decimal Paid { get; set; }
        public decimal Balance { get; set; }
    }
}
