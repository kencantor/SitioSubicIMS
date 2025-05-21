namespace SitioSubicIMS.Web.Models.ViewModels
{
    public class PastDueViewModel
    {
        public string AccountName { get; set; }
        public string BillingNumber { get; set; }
        public string MeterNumber { get; set; }
        public string BillingPeriod { get; set; }
        public DateTime DueDate { get; set; }
        public int DaysOverdue { get; set; }
        public decimal OverDueAmount { get; set; }
        public decimal Paid { get; set; }
        public decimal Balance { get; set; }
    }
}
