using System.ComponentModel.DataAnnotations;

namespace QuarryManagementSystem.Models.Domain
{
    /// <summary>
    /// Raw input materials consumed by production runs (e.g. blasted rock,
    /// quarry feed). Distinct from <see cref="Material"/>, which represents
    /// the finished/sellable goods produced by crushing.
    /// <para/>
    /// Phase 1 keeps this lightweight \u2014 just an ID, name, and unit. Pricing
    /// and procurement metadata can be added later if/when raw rock starts
    /// being purchased rather than self-extracted.
    /// </summary>
    public class RawMaterial
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "Raw Material Name")]
        public string Name { get; set; } = string.Empty;

        [StringLength(20)]
        [Display(Name = "Unit")]
        public string Unit { get; set; } = "Ton";

        [StringLength(20)]
        [Display(Name = "Status")]
        public string Status { get; set; } = "Active";

        [Display(Name = "Created Date")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public virtual ICollection<RawMaterialReceipt> Receipts { get; set; } = new List<RawMaterialReceipt>();
    }
}
