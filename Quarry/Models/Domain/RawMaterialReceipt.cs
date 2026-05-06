using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuarryManagementSystem.Models.Domain
{
    /// <summary>
    /// One delivery of raw material into a quarry yard. Typically created when
    /// blasted rock is brought to the crushing plant; could also represent
    /// purchased raw rock from a third party.
    /// <para/>
    /// Each receipt carries its own unit cost so the weighted-average cost
    /// at the consuming end (the production run) is computed from the actual
    /// money that came in. Phase 1 stores the cost; Phase 4 will post the
    /// matching journal entry (Dr Raw Material Inventory / Cr Cash or Payable).
    /// </summary>
    public class RawMaterialReceipt
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        [Display(Name = "Receipt Number")]
        public string ReceiptNumber { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Quarry")]
        public int QuarryId { get; set; }

        [Required]
        [Display(Name = "Raw Material")]
        public int RawMaterialId { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Receipt Date")]
        public DateTime ReceiptDate { get; set; } = DateTime.Now;

        [Column(TypeName = "decimal(18,3)")]
        [Range(0.001, 9999999.999)]
        [Display(Name = "Quantity")]
        public decimal Quantity { get; set; }

        /// <summary>
        /// Cost per unit (e.g. \u20a6/ton). For self-extracted rock this is the
        /// blasting + transport cost per ton; for purchased rock it's the
        /// invoiced price. Combined with <see cref="Quantity"/> to derive the
        /// total cost added to Raw Material Inventory.
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        [Range(0, 999999.99)]
        [Display(Name = "Unit Cost")]
        public decimal UnitCost { get; set; }

        [NotMapped]
        public decimal TotalCost => Math.Round(Quantity * UnitCost, 2);

        [StringLength(200)]
        [Display(Name = "Source / Supplier")]
        public string? Source { get; set; }

        [StringLength(500)]
        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        [StringLength(100)]
        [Display(Name = "Created By")]
        public string? CreatedBy { get; set; }

        [Display(Name = "Created Date")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public virtual Quarry? Quarry { get; set; }
        public virtual RawMaterial? RawMaterial { get; set; }
    }
}
