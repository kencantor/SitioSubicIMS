using System;
using System.ComponentModel.DataAnnotations;

namespace SitioSubicIMS.Web.Models
{
    public class SMSAlert
    {
        [Key]
        public int SMSAlertID { get; set; }

        [Display(Name = "Allow SMS Alerts")]
        public bool AllowSMSAlerts { get; set; } = false;

        [Display(Name = "Allow Reading Alerts")]
        public bool AllowReadingAlerts { get; set; } = false;

        [Display(Name = "Allow Billing Alerts")]
        public bool AllowBillingAlerts { get; set; } = false;

        [Display(Name = "Allow Payment Alerts")]
        public bool AllowPaymentAlerts { get; set; } = false;

        [Display(Name = "Message Header")]
        public string? MessageHeader { get; set; }

        [Display(Name = "API Key")]
        public string? APIKey { get; set; }

        [Display(Name = "Token")]
        public string? Token { get; set; }

        [Display(Name = "Sender")]
        public string? Sender { get; set; }

        [Display(Name = "Date Created")]
        public DateTime DateCreated { get; set; } = DateTime.Now;

        [Display(Name = "Created By")]
        public string? CreatedBy { get; set; }

        [Display(Name = "Date Updated")]
        public DateTime? DateUpdated { get; set; }

        [Display(Name = "Updated By")]
        public string? UpdatedBy { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
