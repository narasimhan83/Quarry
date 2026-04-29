using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuarryManagementSystem.Models.Domain
{
    public class CustomerPrepayment
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Customer")]
        public int CustomerId { get; set; }

        [Required]
        [StringLength(50)]
        [Display(Name = "Prepayment Number")]
        public string PrepaymentNumber { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Prepayment Date")]
        public DateTime PrepaymentDate { get; set; } = DateTime.Now;

        [DataType(DataType.Date)]
        [Display(Name = "Expected Pickup Date")]
        public DateTime? ExpectedPickupDate { get; set; }

        /// <summary>
        /// Total prepaid amount. For multi-line prepayments this equals the sum of
        /// LineTotal across all LineItems. Kept denormalized for fast listing.
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        [Range(0.01, 999999999.99)]
        [Display(Name = "Amount")]
        public decimal Amount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Used Amount")]
        public decimal UsedAmount { get; set; }

        /// <summary>
        /// Legacy single-material column. Retained for back-compat with prepayments
        /// created before multi-material line items existed. New prepayments should
        /// use LineItems instead. Optional for both.
        /// </summary>
        [Display(Name = "Material")]
        public int? MaterialId { get; set; }

        [StringLength(10)]
        [Display(Name = "Weight Unit")]
        public string? WeightUnit { get; set; }

        [StringLength(50)]
        [Display(Name = "Payment Method (legacy)")]
        public string PaymentMethod { get; set; } = string.Empty;

        /// <summary>
        /// FK to the PaymentMethods lookup table. Nullable for back-compat with
        /// prepayments created before the lookup existed — those rows carry only
        /// the legacy <see cref="PaymentMethod"/> string. New prepayments save
        /// both (FK for clean joins, string for a denormalized label that
        /// survives even if a method is later renamed).
        /// </summary>
        [Display(Name = "Payment Method")]
        public int? PaymentMethodId { get; set; }

        [StringLength(100)]
        [Display(Name = "Reference")]
        public string? Reference { get; set; }

        [StringLength(500)]
        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        [StringLength(20)]
        [Display(Name = "Status")]
        public string Status { get; set; } = "Active"; // Active, Exhausted, Cancelled

        [StringLength(100)]
        [Display(Name = "Created By")]
        public string? CreatedBy { get; set; }

        [Display(Name = "Created Date")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [StringLength(100)]
        [Display(Name = "Updated By")]
        public string? UpdatedBy { get; set; }

        [Display(Name = "Updated Date")]
        public DateTime? UpdatedAt { get; set; }

        [NotMapped]
        [Display(Name = "Remaining Amount")]
        public decimal RemainingAmount => Amount - UsedAmount;

        // Navigation
        public virtual Customer? Customer { get; set; }
        public virtual Material? Material { get; set; }
        public virtual PaymentMethod? PaymentMethodRef { get; set; }
        public virtual ICollection<PrepaymentLineItem> LineItems { get; set; } = new List<PrepaymentLineItem>();
        public virtual ICollection<PrepaymentApplication> Applications { get; set; } = new List<PrepaymentApplication>();
    }

    public class PrepaymentApplication
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Prepayment")]
        public int CustomerPrepaymentId { get; set; }

        /// <summary>
        /// Optional: when the application drains a specific line item (for FIFO
        /// across materials), this records which line was used. For legacy
        /// applications created before line items existed, this is null.
        /// </summary>
        [Display(Name = "Prepayment Line")]
        public int? PrepaymentLineItemId { get; set; }

        [Required]
        [Display(Name = "Invoice")]
        public int InvoiceId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Range(0.01, 999999999.99)]
        [Display(Name = "Applied Amount")]
        public decimal AppliedAmount { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Applied Date")]
        public DateTime AppliedDate { get; set; } = DateTime.Now;

        [StringLength(200)]
        [Display(Name = "Description")]
        public string? Description { get; set; }

        // Navigation
        public virtual CustomerPrepayment? CustomerPrepayment { get; set; }
        public virtual PrepaymentLineItem? PrepaymentLineItem { get; set; }
        public virtual Invoice? Invoice { get; set; }
    }
}
