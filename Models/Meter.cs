using System;
using System.ComponentModel.DataAnnotations;

namespace SitioSubicIMS.Web.Models
{
    public class Meter
    {
        [Key]
        public int MeterID { get; set; }

        [Required(ErrorMessage = "Meter Number is required.")]
        [StringLength(50, ErrorMessage = "Meter Number cannot exceed 50 characters.")]
        [Display(Name = "Meter Number")]
        public string MeterNumber { get; set; }

        [StringLength(100, ErrorMessage = "Serial Number cannot exceed 100 characters.")]
        [Display(Name = "Serial Number")]
        public string SerialNumber { get; set; }

        [StringLength(100, ErrorMessage = "Make cannot exceed 100 characters.")]
        public string Make { get; set; }

        [Required(ErrorMessage = "First Value is required.")]
        [Range(0, long.MaxValue, ErrorMessage = "First Value must be zero or positive.")]
        [Display(Name = "Initial Reading")]
        public long FirstValue { get; set; }

        [Display(Name = "Date Created")]
        public DateTime DateCreated { get; set; } = DateTime.Now;

        [Display(Name = "Created By")]
        public string? CreatedBy { get; set; }

        [Display(Name = "Date Updated")]
        public DateTime? DateUpdated { get; set; }

        [Display(Name = "Updated By")]
        public string? UpdatedBy { get; set; }

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; } = true;
    }
}
