using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuarryManagementSystem.Models.Domain
{
    /// <summary>
    /// Running weighted-average cost cache, keyed by (Quarry, Material-or-RawMaterial).
    /// Updated synchronously whenever a <see cref="StockMovement"/> is posted
    /// so reads are O(1). Could be derived from the movement log if ever lost,
    /// but storing it explicitly avoids hot-path aggregation on the weighbridge.
    /// <para/>
    /// Update math when a positive-quantity movement is posted:
    ///   newQty       = oldQty + movementQty
    ///   newTotalCost = oldTotalCost + (movementQty \u00d7 movementUnitCost)
    ///   newAvgCost   = newTotalCost / newQty   (or 0 when newQty == 0)
    /// <para/>
    /// On a negative-quantity movement (sale, consumption, adjustment-down):
    ///   newQty       = oldQty + movementQty   (movementQty is negative)
    ///   newTotalCost = oldTotalCost + (movementQty \u00d7 oldAvgCost)
    ///   AvgCost is unchanged \u2014 sales draw at the prevailing average and
    ///   the running average stays the same until the next receipt.
    /// <para/>
    /// MaterialId XOR RawMaterialId, just like StockMovement. Two rows for the
    /// same yard if you happen to track both finished and raw at that yard.
    /// </summary>
    public class MaterialCostState
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Quarry")]
        public int QuarryId { get; set; }

        [Display(Name = "Material")]
        public int? MaterialId { get; set; }

        [Display(Name = "Raw Material")]
        public int? RawMaterialId { get; set; }

        [Column(TypeName = "decimal(18,3)")]
        [Display(Name = "Quantity On Hand")]
        public decimal QuantityOnHand { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Total Cost On Hand")]
        public decimal TotalCostOnHand { get; set; } = 0m;

        [NotMapped]
        public decimal AverageUnitCost
            => QuantityOnHand > 0 ? Math.Round(TotalCostOnHand / QuantityOnHand, 4) : 0m;

        [Display(Name = "Last Updated")]
        public DateTime LastUpdated { get; set; } = DateTime.Now;

        public virtual Quarry? Quarry { get; set; }
        public virtual Material? Material { get; set; }
        public virtual RawMaterial? RawMaterial { get; set; }
    }
}
