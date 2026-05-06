using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuarryManagementSystem.Models.Domain
{
    /// <summary>
    /// One finished-material output line on a <see cref="ProductionRun"/>.
    /// Quantity is what came out of the crusher for that grade; AllocatedCost
    /// is its share of the total input cost, computed at post-time using the
    /// weight-share rule and frozen on the row so subsequent runs / WAC
    /// updates don't restate history.
    /// </summary>
    public class ProductionRunOutput
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Production Run")]
        public int ProductionRunId { get; set; }

        [Required]
        [Display(Name = "Material")]
        public int MaterialId { get; set; }

        [Column(TypeName = "decimal(18,3)")]
        [Range(0.001, 9999999.999)]
        [Display(Name = "Quantity")]
        public decimal Quantity { get; set; }

        /// <summary>
        /// Frozen cost of this output line. Set when the run is Posted using:
        ///   AllocatedCost = run.InputTotalCost \u00d7 (this.Quantity / run.TotalOutputQuantity)
        /// Stored on the row so a later edit to WAC or to another run doesn't
        /// change the historical cost on this line.
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Allocated Cost")]
        public decimal AllocatedCost { get; set; }

        [NotMapped]
        public decimal UnitCost => Quantity > 0 ? Math.Round(AllocatedCost / Quantity, 4) : 0m;

        public virtual ProductionRun? ProductionRun { get; set; }
        public virtual Material? Material { get; set; }
    }
}
