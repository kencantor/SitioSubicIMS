namespace SitioSubicIMS.Web.Models
{
    public enum BillingStatus
    {
        Pending = 0,
        Unpaid,
        Paid,
        Overdue,
        Cancelled
    }
    public enum PaymentMethod
    {
        Cash = 0,
        Online,
        Check,
    }
    public enum PaymentStatus
    {
        Unposted = 0,
        Posted
    }
}
