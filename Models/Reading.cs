using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SitioSubicIMS.Web.Models
{
    public class Reading
    {
        [Key]
        public int ReadingID { get; set; }

        [Display(Name = "User")]
        public string? UserID { get; set; }

        [Required]
        [Display(Name = "Meter")]
        public int MeterID { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Reading Date")]
        public DateTime ReadingDate { get; set; }

        [Required]
        [Range(0, long.MaxValue, ErrorMessage = "Reading value must be a positive number.")]
        [Display(Name = "Reading Value")]
        public long ReadingValue { get; set; }

        [Required]
        [Range(1, 12, ErrorMessage = "Billing month must be between 1 and 12.")]
        [Display(Name = "Billing Month")]
        public int BillingMonth { get; set; }

        [Required]
        [Range(2000, 2100, ErrorMessage = "Enter a valid billing year.")]
        [Display(Name = "Billing Year")]
        public int BillingYear { get; set; }

        [Display(Name = "Is Billed")]
        public bool IsBilled { get; set; } = false;

        [Display(Name = "Date Created")]
        public DateTime DateCreated { get; set; } = DateTime.Now;

        [MaxLength(100)]
        [Display(Name = "Created By")]
        public string? CreatedBy { get; set; }

        [Display(Name = "Date Updated")]
        public DateTime? DateUpdated { get; set; }

        [MaxLength(100)]
        [Display(Name = "Updated By")]
        public string? UpdatedBy { get; set; }

        public bool IsActive { get; set; } = true;

        // Navigation Properties (optional)
        [ForeignKey("UserID")]
        public ApplicationUser? User { get; set; }

        [ForeignKey("MeterID")]
        public Meter? Meter { get; set; }

        public decimal PreviousReadingValue { get; set; }

        [NotMapped]
        public decimal Consumption => ReadingValue - PreviousReadingValue;
    }
}
