using System.ComponentModel.DataAnnotations;

namespace QuarryManagementSystem.ViewModels
{
    /// <summary>
    /// Read-only stock-on-hand summary, one row per (Quarry × Material-or-RawMaterial).
    /// Sourced from MaterialCostState — the running cache that the inventory
    /// service updates in lockstep with each StockMovement. Not aggregated
    /// from movements at query time; that's intentional, the cache is the
    /// authoritative read path for performance reasons.
    /// </summary>
    public class StockOnHandRow
    {
        public int QuarryId { get; set; }

        [Display(Name = "Quarry")]
        public string QuarryName { get; set; } = string.Empty;

        [Display(Name = "Item")]
        public string ItemName { get; set; } = string.Empty;

        /// <summary>"Finished" or "Raw" — drives the icon/label in the view.</summary>
        public string Kind { get; set; } = string.Empty;

        [Display(Name = "Quantity On Hand")]
        public decimal QuantityOnHand { get; set; }

        [Display(Name = "Total Cost")]
        public decimal TotalCostOnHand { get; set; }

        [Display(Name = "Average Unit Cost")]
        public decimal AverageUnitCost
            => QuantityOnHand > 0 ? Math.Round(TotalCostOnHand / QuantityOnHand, 4) : 0m;

        [Display(Name = "Last Updated")]
        public DateTime LastUpdated { get; set; }
    }

    public class StockOnHandViewModel
    {
        public List<StockOnHandRow> Rows { get; set; } = new();

        public decimal TotalValue => Rows.Sum(r => r.TotalCostOnHand);

        // Filter state, echoed back to the form.
        public int? QuarryFilter { get; set; }
        public string? KindFilter { get; set; } // "All" / "Finished" / "Raw"
    }
}
