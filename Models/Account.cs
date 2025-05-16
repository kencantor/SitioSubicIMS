using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SitioSubicIMS.Web.Models
{
    public class Account
    {
        [Key]
        public int AccountID { get; set; }

        [Required(ErrorMessage = "Meter is required.")]
        [ForeignKey("Meter")]
        public int MeterID { get; set; }

        public Meter? Meter { get; set; }

        [Required(ErrorMessage = "Account Number is required.")]
        public string AccountNumber { get; set; } = "Auto-Generated";// Will be auto-generated

        [Required(ErrorMessage = "Account Name is required.")]
        [StringLength(100, ErrorMessage = "Account Name must be at most 100 characters.")]
        public string AccountName { get; set; }

        [Required(ErrorMessage = "Mobile Number is required.")]
        [RegularExpression(@"^(09|\+639)\d{9}$", ErrorMessage = "Mobile Number must be a valid PH format (e.g., 09171234567 or +639171234567).")]
        public string ContactNumber { get; set; }

        [Required(ErrorMessage = "Address is required.")]
        public string Address { get; set; }

        public DateTime DateCreated { get; set; } = DateTime.Now;

        public string? CreatedBy { get; set; }

        public DateTime? DateUpdated { get; set; }

        public string? UpdatedBy { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
