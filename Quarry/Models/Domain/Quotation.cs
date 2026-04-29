using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using QuarryManagementSystem.Utils;

namespace QuarryManagementSystem.Models.Domain
{
    public class Quotation
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        [Display(Name = "Quotation Number")]
        public string QuotationNumber { get; set; } = string.Empty;

        [Display(Name = "Customer")]
        public int CustomerId { get; set; }

        [Required]
        [Display(Name = "Quotation Date")]
        public DateTime QuotationDate { get; set; } = DateTime.Now;

        [Display(Name = "Expiry Date")]
        public DateTime? ExpiryDate { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Subtotal")]
        public decimal SubTotal { get; set; }

        /// <summary>
        /// Flat customer rebate auto-applied when the quotation is created, taken
        /// from Customer.RebateAmount if HasRebate is true. Stored so future edits
        /// and the print view can show "Rebate: −₦X" even if the customer's
        /// rebate settings change later. Treated as a discount before VAT.
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Rebate Amount")]
        public decimal RebateAmount { get; set; } = 0m;

        /// <summary>
        /// Flat transport fee auto-applied when the quotation is created, taken
        /// from Customer.TransportAmount if TransportRequired is true. Added
        /// after the subtotal (and after the rebate), before VAT.
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Transport Amount")]
        public decimal TransportAmount { get; set; } = 0m;

        /// <summary>
        /// Snapshot of the customer's VAT type (Exclusive or Inclusive) at the time
        /// the quotation was created. Exclusive = VAT added on top of net; Inclusive
        /// = VAT already embedded in line prices. Stored so the print and edit views
        /// can label VAT correctly even if the customer's VAT type is later changed.
        /// </summary>
        [StringLength(20)]
        [Display(Name = "VAT Type")]
        public string? VatTypeSnapshot { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "VAT Amount")]
        public decimal VatAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Total Amount")]
        public decimal TotalAmount { get; set; }

        [StringLength(20)]
        [Display(Name = "Status")]
        public string Status { get; set; } = "Draft"; // Draft, Sent, Accepted, Rejected, Cancelled, Expired

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
        public virtual ICollection<QuotationItem> Items { get; set; } = new List<QuotationItem>();

        // Helper methods
        public bool IsExpired()
        {
            return ExpiryDate.HasValue && ExpiryDate.Value.Date < DateTime.Now.Date && Status != "Accepted";
        }

        public string GetAmountInWords()
        {
            return NumberToWordsConverter.ConvertAmountToWords(TotalAmount);
        }
    }
}