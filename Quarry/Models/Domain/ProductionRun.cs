using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuarryManagementSystem.Models.Domain
{
    /// <summary>
    /// Header for one crushing operation: consumes raw rock, produces one or
    /// more finished materials (Dust, 1/2 inch, 3/4 inch, etc.).
    /// <para/>
    /// Mass balance: Sum(Outputs.Quantity) + WasteQuantity == InputQuantity.
    /// Phase 1 stores all three; Phase 2 enforces the equation in the
    /// controller before save. Status transitions (Draft \u2192 Posted \u2192
    /// Cancelled) gate the journal posting in Phase 4.
    /// <para/>
    /// Cost allocation across outputs uses the weight-share method:
    ///   per-output cost = TotalInputCost \u00d7 (output.Quantity / sum_of_output_quantities)
    /// Waste is treated as a production loss (Phase 4 posts it to Production
    /// Variance, account 5002).
    /// </summary>
    public class ProductionRun
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        [Display(Name = "Run Number")]
        public string RunNumber { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Quarry")]
        public int QuarryId { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Run Date")]
        public DateTime RunDate { get; set; } = DateTime.Now;

        [Required]
        [Display(Name = "Raw Material")]
        public int RawMaterialId { get; set; }

        [Column(TypeName = "decimal(18,3)")]
        [Range(0.001, 9999999.999)]
        [Display(Name = "Input Quantity")]
        public decimal InputQuantity { get; set; }

        /// <summary>
        /// Total cost of the consumed raw material, drawn from the running
        /// weighted-average cost on the raw material inventory at run time.
        /// Cached on the run so subsequent restatements of WAC don't change
        /// historical production costs.
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Input Total Cost")]
        public decimal InputTotalCost { get; set; }

        [Column(TypeName = "decimal(18,3)")]
        [Range(0, 9999999.999)]
        [Display(Name = "Waste Quantity")]
        public decimal WasteQuantity { get; set; }

        [StringLength(20)]
        [Display(Name = "Status")]
        public string Status { get; set; } = "Draft"; // Draft, Posted, Cancelled

        [StringLength(200)]
        [Display(Name = "Operator")]
        public string? Operator { get; set; }

        [StringLength(500)]
        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        [StringLength(100)]
        [Display(Name = "Created By")]
        public string? CreatedBy { get; set; }

        [Display(Name = "Created Date")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Display(Name = "Posted Date")]
        public DateTime? PostedAt { get; set; }

        public virtual Quarry? Quarry { get; set; }
        public virtual RawMaterial? RawMaterial { get; set; }
        public virtual ICollection<ProductionRunOutput> Outputs { get; set; } = new List<ProductionRunOutput>();

        /// <summary>
        /// Sum of output quantities. Used by mass-balance validation and
        /// cost-allocation math. NotMapped \u2014 derived from the Outputs collection.
        /// </summary>
        [NotMapped]
        public decimal TotalOutputQuantity => Outputs?.Sum(o => o.Quantity) ?? 0m;

        [NotMapped]
        public decimal MassBalanceDelta => InputQuantity - TotalOutputQuantity - WasteQuantity;
    }
}
