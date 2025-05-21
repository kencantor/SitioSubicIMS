namespace SitioSubicIMS.Web.Models.ViewModels
{
    public class PaymentsCollectedViewModel
    {
        public string AccountName { get; set; }
        public string PaymentNumber { get; set; }
        public string BillingNumber { get; set; }
        public DateTime PaymentDate { get; set; }
        public string PaymentMethod { get; set; }
        public decimal AmountPaid { get; set; }
    }
}
