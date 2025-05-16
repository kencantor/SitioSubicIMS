namespace SitioSubicIMS.Web.Models.ViewModels
{
    public class PaymentFormViewModel
    {
        public List<UnpaidBillingInfo> UnpaidBillings { get; set; }

        public Billing SelectedBilling { get; set; }
        public Account SelectedAccount { get; set; }

        public decimal ExistingPaymentsTotal { get; set; }
        public decimal Balance { get; set; }

        public Payment NewPayment { get; set; }
    }
}
