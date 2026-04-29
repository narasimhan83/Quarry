using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuarryManagementSystem.Models.Domain
{
    /// <summary>
    /// A line item within a CustomerPrepayment. A single prepayment can cover
    /// multiple materials, each with its own quantity, unit, and unit price.
    /// When invoices apply the prepayment, each line's UsedAmount drains in FIFO
    /// order (oldest prepayment first, lowest line Id within a prepayment).
    /// </summary>
    public class PrepaymentLineItem
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Prepayment")]
        public int CustomerPrepaymentId { get; set; }

        [Required]
        [Display(Name = "Material")]
        public int MaterialId { get; set; }

        [Column(TypeName = "decimal(18,3)")]
        [Range(0.001, 9999999.999, ErrorMessage = "Quantity must be greater than zero.")]
        [Display(Name = "Quantity")]
        public decimal Quantity { get; set; }

        [Required]
        [StringLength(20)]
        [Display(Name = "Unit")]
        public string Unit { get; set; } = "Ton";

        [Column(TypeName = "decimal(18,2)")]
        [Range(0.01, 999999999.99, ErrorMessage = "Unit price must be greater than zero.")]
        [Display(Name = "Unit Price")]
        public decimal UnitPrice { get; set; }

        /// <summary>
        /// VAT amount already embedded in UnitPrice/LineTotal at the time the
        /// prepayment was created. Preserved for audit purposes so we can always
        /// answer "how much VAT did the customer pay in this prepayment?", even
        /// if the customer's VAT type is later changed. Zero when no VAT applied
        /// (Inclusive customers typically show 0 here because the backed-out VAT
        /// share wasn't broken out at creation time).
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "VAT Amount")]
        public decimal VatAmount { get; set; } = 0m;

        /// <summary>
        /// Per-unit rebate that was subtracted from the catalog price at creation
        /// time. Stored so the Edit view and receipt can show "Rebate ₦X per unit
        /// applied" even after the customer's rebate settings change.
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Rebate Amount")]
        public decimal RebateAmount { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Line Total")]
        public decimal LineTotal { get; set; }

        [Column(TypeName = "decimal(18,3)")]
        [Display(Name = "Used Quantity")]
        public decimal UsedQuantity { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Used Amount")]
        public decimal UsedAmount { get; set; } = 0m;

        [Display(Name = "Created Date")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation
        public virtual CustomerPrepayment? CustomerPrepayment { get; set; }
        public virtual Material? Material { get; set; }

        [NotMapped]
        [Display(Name = "Remaining Amount")]
        public decimal RemainingAmount => LineTotal - UsedAmount;

        [NotMapped]
        [Display(Name = "Remaining Quantity")]
        public decimal RemainingQuantity => Quantity - UsedQuantity;
    }
}
