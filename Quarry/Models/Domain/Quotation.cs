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