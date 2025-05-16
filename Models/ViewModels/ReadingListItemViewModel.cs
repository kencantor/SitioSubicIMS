namespace SitioSubicIMS.Web.Models.ViewModels
{
    public class ReadingListItemViewModel
    {
        public int ReadingId { get; set; }
        public string AccountNumber { get; set; }
        public string MeterNumber { get; set; }
        public DateTime ReadingDate { get; set; }
        public decimal PreviousReading { get; set; }
        public decimal CurrentReading { get; set; }
        public decimal Consumption { get; set; }
        public string Status { get; set; }
        public DateTime DateCreated { get; set; }
        public bool IsActive { get; set; }
        public int BillingMonth { get; set; }
        public int BillingYear { get; set; }
        public bool IsBilled { get; set; }

        public string BillingPeriod => $"{System.Globalization.CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(BillingMonth)} {BillingYear}";
    }
}
