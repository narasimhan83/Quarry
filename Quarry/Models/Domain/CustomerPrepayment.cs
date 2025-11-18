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

        [Column(TypeName = "decimal(18,2)")]
        [Range(0.01, 999999999.99)]
        [Display(Name = "Amount")]
        public decimal Amount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Used Amount")]
        public decimal UsedAmount { get; set; }

        [StringLength(50)]
        [Display(Name = "Payment Method")]
        public string PaymentMethod { get; set; } = string.Empty;

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
        public virtual ICollection<PrepaymentApplication> Applications { get; set; } = new List<PrepaymentApplication>();
    }

    public class PrepaymentApplication
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Prepayment")]
        public int CustomerPrepaymentId { get; set; }

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
        public virtual Invoice? Invoice { get; set; }
    }
}