using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuarryManagementSystem.Models.Domain
{
    public class QuotationItem
    {
        public int Id { get; set; }

        [Display(Name = "Quotation")]
        public int QuotationId { get; set; }

        [Display(Name = "Material")]
        public int? MaterialId { get; set; }

        [StringLength(200)]
        [Display(Name = "Description")]
        public string? Description { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Range(typeof(decimal), "0.01", "999999999.99")]
        [Display(Name = "Quantity")]
        public decimal Quantity { get; set; }

        [StringLength(20)]
        [Display(Name = "Unit")]
        public string Unit { get; set; } = "Ton";

        [Column(TypeName = "decimal(18,2)")]
        [Range(typeof(decimal), "0.00", "999999999.99")]
        [Display(Name = "Unit Price")]
        public decimal UnitPrice { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        [Range(typeof(decimal), "0.00", "100.00")]
        [Display(Name = "VAT Rate (%)")]
        public decimal VatRate { get; set; } = 7.5m;

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Subtotal")]
        public decimal LineSubTotal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "VAT Amount")]
        public decimal LineVatAmount { get; set; }

        /// <summary>
        /// Per-line share of the quotation's flat rebate. Header rebate is
        /// distributed across lines proportionally to each line's subtotal. Stored
        /// for audit / display so the Edit view can show "Rebate ₦X applied" per
        /// line, mirroring the prepayment line-item storage pattern.
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Rebate Amount")]
        public decimal LineRebateAmount { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Total")]
        public decimal LineTotal { get; set; }

        // Navigation
        public virtual Quotation Quotation { get; set; } = null!;
        public virtual Material? Material { get; set; }

        // Helper
        public void Recalculate()
        {
            LineSubTotal = Quantity * UnitPrice;
            LineVatAmount = LineSubTotal * (VatRate / 100m);
            LineTotal = LineSubTotal + LineVatAmount;
        }
    }
}