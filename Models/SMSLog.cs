namespace SitioSubicIMS.Web.Models
{
    public class SMSLog
    {
        public int SMSLogID { get; set; }
        public string PhoneNumber { get; set; }
        public string Message { get; set; }
        public string Status { get; set; }  // "Success" or "Failed"
        public string ErrorMessage { get; set; }
        public DateTime DateSent { get; set; }
        public string CreatedBy { get; set; }
    }
}
