namespace SitioSubicIMS.Web.Models.ViewModels
{
    public class ReportDashboardViewModel
    {
        // Existing properties...
        public int TotalAccounts { get; set; }
        public decimal TotalBillings { get; set; }
        public int UnpaidBillingsCount { get; set; }
        public decimal TotalPayments { get; set; }
        public int PastDues { get; set; }

        // New properties for chart
        public List<string> ChartLabels { get; set; } = new List<string>(); // e.g., "Nov 2024"
        public List<decimal> MonthlyBillings { get; set; } = new List<decimal>();
        public List<decimal> MonthlyPayments { get; set; } = new List<decimal>();
    }

}
