using SitioSubicIMS.Web.Models;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

public class Billing
{
    [Key]
    public int BillingID { get; set; }

    [Required]
    [StringLength(20)]
    public string BillingNumber { get; set; }

    [Required]
    public int ReadingID { get; set; }

    [ForeignKey("ReadingID")]
    public Reading Reading { get; set; }

    [Required]
    public DateTime BillingDate { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal RatePerCubicMeter { get; set; }

    [Required]
    public int MinimumConsumption { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal MinimumCharge { get; set; }

    [Required]
    [Column(TypeName = "decimal(5,2)")]
    public decimal PenaltyRate { get; set; }

    [Required]
    [Column(TypeName = "decimal(5,2)")]
    public decimal VATRate { get; set; }

    [Required]
    public DateTime DueDate { get; set; }

    public DateTime? DisconnectionDate { get; set; }

    public BillingStatus BillingStatus { get; set; } = BillingStatus.Pending;

    public DateTime DateCreated { get; set; } = DateTime.Now;

    [StringLength(100)]
    public string? CreatedBy { get; set; }

    public DateTime? DateUpdated { get; set; }

    [StringLength(100)]
    public string? UpdatedBy { get; set; }

    public bool IsActive { get; set; } = true;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Arrears { get; set; }

    // ---- Derived Calculations ----

    [NotMapped]
    public decimal BaseAmount
    {
        get
        {
            var consumption = Reading?.Consumption ?? 0;
            return consumption > MinimumConsumption
                ? consumption * RatePerCubicMeter
                : MinimumCharge;
        }
    }

    [NotMapped]
    public decimal VATAmount => VATRate > 0 ? BaseAmount * VATRate : 0;

    [NotMapped]
    public decimal DueAmount => BaseAmount + VATAmount + Arrears;

    [NotMapped]
    public decimal Penalty => PenaltyRate > 0 ? DueAmount * PenaltyRate : 0;

    [NotMapped]
    public decimal OverDueAmount => DueAmount + Penalty;
}
