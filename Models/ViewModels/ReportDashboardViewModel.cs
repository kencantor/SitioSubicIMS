namespace SitioSubicIMS.Web.Models.ViewModels
{
    public class ReportDashboardViewModel
    {
        public int TotalAccounts { get; set; }
        public int UnpaidBillingsCount { get; set; }
        public decimal TotalBillings { get; set; }
        public decimal TotalPayments { get; set; }
        public int PastDues { get; set; }
    }
}
