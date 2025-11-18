using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using QuarryManagementSystem.Utils;

namespace QuarryManagementSystem.Models.Domain
{
    public class Invoice
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        [Display(Name = "Invoice Number")]
        public string InvoiceNumber { get; set; } = string.Empty;

        [Display(Name = "Customer")]
        public int CustomerId { get; set; }

        [Display(Name = "Weighment Transaction")]
        public int? WeighmentId { get; set; }

        [Required]
        [Display(Name = "Invoice Date")]
        public DateTime InvoiceDate { get; set; } = DateTime.Now;

        [Display(Name = "Due Date")]
        public DateTime? DueDate { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Subtotal")]
        public decimal SubTotal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "VAT Amount")]
        public decimal VatAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Total Amount")]
        public decimal TotalAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Paid Amount")]
        public decimal PaidAmount { get; set; } = 0;
 
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Prepayment Applied")]
        public decimal PrepaymentApplied { get; set; } = 0;
 
        [Display(Name = "Fully Prepaid")]
        public bool IsFullyPrepaid { get; set; } = false;
 
        [NotMapped]
        [Display(Name = "Outstanding Balance")]
        public decimal OutstandingBalance => TotalAmount - PaidAmount;
 
        [StringLength(20)]
        [Display(Name = "Status")]
        public string Status { get; set; } = "Unpaid"; // Unpaid, Paid, Overdue, Cancelled

        [StringLength(100)]
        [Display(Name = "Payment Terms")]
        public string? PaymentTerms { get; set; } // 30 days, 60 days, etc.

        [StringLength(50)]
        [Display(Name = "LGA Receipt Number")]
        public string? LGAReceiptNumber { get; set; } // For local government receipts

        [StringLength(500)]
        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        // Audit Trail
        [StringLength(100)]
        [Display(Name = "Created By")]
        public string? CreatedBy { get; set; }

        [Display(Name = "Created Date")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Display(Name = "Updated Date")]
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public virtual Customer Customer { get; set; } = null!;
        public virtual WeighmentTransaction? WeighmentTransaction { get; set; }
        public virtual ApplicationUser? CreatedByUser { get; set; }
        public virtual ICollection<PrepaymentApplication> PrepaymentApplications { get; set; } = new List<PrepaymentApplication>();

        // Helper methods
        public bool IsPaid()
        {
            return Status == "Paid" || OutstandingBalance <= 0;
        }

        public bool IsOverdue()
        {
            return DueDate.HasValue && DueDate.Value < DateTime.Now && !IsPaid();
        }

        public bool IsFullyPaid()
        {
            return PaidAmount >= TotalAmount;
        }

        public decimal GetOutstandingAmount()
        {
            return Math.Max(0, TotalAmount - PaidAmount);
        }

        public void UpdateStatus()
        {
            if (IsFullyPaid())
            {
                Status = "Paid";
            }
            else if (IsOverdue())
            {
                Status = "Overdue";
            }
            else
            {
                Status = "Unpaid";
            }
        }

        public string GetAmountInWords()
        {
            return NumberToWordsConverter.ConvertAmountToWords(TotalAmount);
        }

        public TimeSpan? GetOverdueDays()
        {
            if (IsOverdue() && DueDate.HasValue)
            {
                return DateTime.Now - DueDate.Value;
            }
            return null;
        }

        public decimal GetLatePaymentPenalty(decimal penaltyRate = 0.02m) // 2% monthly
        {
            if (IsOverdue())
            {
                var overdueDays = GetOverdueDays();
                if (overdueDays.HasValue)
                {
                    var monthsOverdue = (decimal)overdueDays.Value.TotalDays / 30;
                    return OutstandingBalance * penaltyRate * (decimal)Math.Ceiling(monthsOverdue);
                }
            }
            return 0;
        }
    }
}