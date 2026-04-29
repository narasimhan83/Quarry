using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuarryManagementSystem.Models.Domain
{
    /// <summary>
    /// One price-change row for a (Customer, Material) pair. Multiple rows are
    /// kept as history; the "current" price is simply the row with the largest
    /// EffectiveFrom date that is not in the future. Null IsCurrent cuts down
    /// on query-time work but is just a denormalized flag — EffectiveFrom is
    /// the source of truth.
    /// </summary>
    public class CustomerMaterialPrice
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Customer")]
        public int CustomerId { get; set; }

        [Required]
        [Display(Name = "Material")]
        public int MaterialId { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        [Range(0.01, 999999999.99, ErrorMessage = "Unit price must be greater than zero.")]
        [Display(Name = "Unit Price")]
        public decimal UnitPrice { get; set; }

        /// <summary>
        /// Optional per-customer VAT rate override. When null the material's
        /// VatRate (or VAT type rules) applies.
        /// </summary>
        [Column(TypeName = "decimal(5,2)")]
        [Range(0, 100)]
        [Display(Name = "VAT Rate (%)")]
        public decimal? VatRate { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Effective From")]
        public DateTime EffectiveFrom { get; set; } = DateTime.Today;

        /// <summary>
        /// Denormalized "this is the current price" flag. Maintained by the
        /// pricing service: only one row per (CustomerId, MaterialId) is marked
        /// current at any given time.
        /// </summary>
        [Display(Name = "Is Current")]
        public bool IsCurrent { get; set; } = true;

        [StringLength(200)]
        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        [StringLength(100)]
        [Display(Name = "Created By")]
        public string? CreatedBy { get; set; }

        [Display(Name = "Created Date")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation
        public virtual Customer? Customer { get; set; }
        public virtual Material? Material { get; set; }
    }
}
