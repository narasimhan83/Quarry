using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuarryManagementSystem.Models.Domain
{
    /// <summary>
    /// Single source of truth for every change in inventory quantity / cost.
    /// All paths that touch stock \u2014 raw material receipts, production runs,
    /// weighbridge sales, transfers between yards, manual adjustments \u2014\n    /// write a row here. The current state on <see cref="StockYard"/> and
    /// <see cref="MaterialCostState"/> is derivable from these rows; we cache
    /// it for fast reads but the movements are the audit trail.
    /// <para/>
    /// Quantity sign:
    ///   Receipts / Production-out / Transfer-in / Positive adjustment \u2192 positive
    ///   Sale / Production-in (raw consumption) / Transfer-out / Negative adjustment \u2192 negative
    /// <para/>
    /// MaterialId XOR RawMaterialId is set, never both \u2014 a movement is for
    /// finished goods or raw materials, not both at once. Phase 1 keeps this
    /// as a soft rule (validated in services); we don't enforce a check
    /// constraint at the DB level until Phase 2 controllers exist.
    /// </summary>
    public class StockMovement
    {
        public int Id { get; set; }

        [Required]
        [DataType(DataType.DateTime)]
        [Display(Name = "Movement Date")]
        public DateTime MovementDate { get; set; } = DateTime.Now;

        [Required]
        [Display(Name = "Quarry")]
        public int QuarryId { get; set; }

        /// <summary>Set when the movement is for finished goods.</summary>
        [Display(Name = "Material")]
        public int? MaterialId { get; set; }

        /// <summary>Set when the movement is for raw inputs.</summary>
        [Display(Name = "Raw Material")]
        public int? RawMaterialId { get; set; }

        [Required]
        [StringLength(30)]
        [Display(Name = "Movement Type")]
        public string MovementType { get; set; } = string.Empty;
        // Allowed values:
        //   "RawReceipt"        \u2014 raw rock arrived at yard
        //   "ProductionInput"   \u2014 raw rock consumed by a run (negative qty)
        //   "ProductionOutput"  \u2014 finished material produced by a run (positive qty)
        //   "Sale"              \u2014 weighbridge dispatch (negative qty)
        //   "TransferOut"       \u2014 stock leaving this yard (negative qty)
        //   "TransferIn"        \u2014 stock arriving at this yard (positive qty)
        //   "AdjustmentPlus"    \u2014 manual add (stocktake correction up)
        //   "AdjustmentMinus"   \u2014 manual remove (stocktake correction down, shrinkage)

        [Column(TypeName = "decimal(18,3)")]
        [Display(Name = "Quantity")]
        public decimal Quantity { get; set; }

        /// <summary>
        /// Per-unit cost applied to this movement. For receipts this is the
        /// purchase / production cost; for sales this is the weighted-average
        /// cost at the moment the sale was posted. Frozen on the row.
        /// </summary>
        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "Unit Cost")]
        public decimal UnitCost { get; set; }

        [NotMapped]
        public decimal TotalCost => Math.Round(Quantity * UnitCost, 2);

        // Cross-references back to the source document so we can audit-trail
        // each movement. Exactly one of these is set per row, depending on
        // MovementType. All nullable.
        [Display(Name = "Raw Material Receipt")]
        public int? RawMaterialReceiptId { get; set; }

        [Display(Name = "Production Run")]
        public int? ProductionRunId { get; set; }

        [Display(Name = "Production Run Output")]
        public int? ProductionRunOutputId { get; set; }

        [Display(Name = "Weighment Transaction")]
        public int? WeighmentTransactionId { get; set; }

        [StringLength(500)]
        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        [StringLength(100)]
        [Display(Name = "Created By")]
        public string? CreatedBy { get; set; }

        [Display(Name = "Created Date")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public virtual Quarry? Quarry { get; set; }
        public virtual Material? Material { get; set; }
        public virtual RawMaterial? RawMaterial { get; set; }
        public virtual RawMaterialReceipt? RawMaterialReceipt { get; set; }
        public virtual ProductionRun? ProductionRun { get; set; }
        public virtual ProductionRunOutput? ProductionRunOutput { get; set; }
        public virtual WeighmentTransaction? WeighmentTransaction { get; set; }
    }
}
