using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SitioSubicIMS.Web.Models
{
    public class Configuration
    {
        [Key]
        public int ConfigurationID { get; set; }

        [Required]
        [Column(TypeName = "decimal(18, 4)")]
        [Range(0, double.MaxValue, ErrorMessage = "Price per cubic meter must be non-negative.")]
        public decimal PricePerCubicMeter { get; set; }

        [Required]
        [Range(0, int.MaxValue, ErrorMessage = "Minimum consumption must be non-negative.")]
        public int MinimumConsumption { get; set; }

        [Required]
        [Column(TypeName = "decimal(18, 2)")]
        [Range(0, double.MaxValue, ErrorMessage = "Minimum charge must be non-negative.")]
        public decimal MinimumCharge { get; set; }

        [Required]
        [Column(TypeName = "decimal(5, 4)")]
        [Range(0, 1, ErrorMessage = "Penalty rate must be between 0 and 1 (e.g. 0.05 = 5%).")]
        public decimal PenaltyRate { get; set; }

        [Required]
        [Column(TypeName = "decimal(5, 4)")]
        [Range(0, 1, ErrorMessage = "VAT rate must be between 0 and 1 (e.g. 0.12 = 12%).")]
        public decimal VATRate { get; set; }

        [Required]
        public DateTime DateCreated { get; set; } = DateTime.Now;

        [MaxLength(100)]
        public string? CreatedBy { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
