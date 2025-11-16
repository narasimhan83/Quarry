using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuarryManagementSystem.Models.Domain
{
    public class Material
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Material name is required")]
        [StringLength(100)]
        [Display(Name = "Material Name")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Material type is required")]
        [StringLength(50)]
        [Display(Name = "Material Type")]
        public string Type { get; set; } = string.Empty; // Aggregate, Sand, Dust, Laterite

        [Required(ErrorMessage = "Unit of measurement is required")]
        [StringLength(20)]
        [Display(Name = "Unit")]
        public string Unit { get; set; } = "Ton";

        [Required(ErrorMessage = "Unit price is required")]
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Unit Price")]
        [Range(0.01, 999999.99, ErrorMessage = "Unit price must be between 0.01 and 999,999.99")]
        public decimal UnitPrice { get; set; }

        [Required(ErrorMessage = "VAT rate is required")]
        [Column(TypeName = "decimal(5,2)")]
        [Display(Name = "VAT Rate (%)")]
        [Range(0, 100, ErrorMessage = "VAT rate must be between 0 and 100")]
        public decimal VatRate { get; set; } = 7.5m; // Nigeria VAT Rate

        [StringLength(20)]
        public string Status { get; set; } = "Active";

        [Display(Name = "Created Date")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Display(Name = "Updated Date")]
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public virtual ICollection<WeighmentTransaction> WeighmentTransactions { get; set; } = new List<WeighmentTransaction>();
        public virtual ICollection<StockYard> StockYards { get; set; } = new List<StockYard>();

        // Helper properties
        [NotMapped]
        [Display(Name = "Price with VAT")]
        public decimal PriceWithVat => UnitPrice * (1 + VatRate / 100);

        [NotMapped]
        [Display(Name = "VAT Amount")]
        public decimal VatAmount => UnitPrice * (VatRate / 100);

        // Common Nigerian quarry materials
        public static readonly string[] MaterialTypes = 
        {
            "Aggregate",
            "Sand", 
            "Dust",
            "Laterite",
            "Stone",
            "Gravel"
        };

        public static readonly string[] CommonMaterials =
        {
            "Granite Aggregate 20mm",
            "Granite Aggregate 10mm", 
            "Sharp Sand",
            "Plaster Sand",
            "Quarry Dust",
            "Laterite",
            "Granite Chippings 5mm",
            "Granite Chippings 3mm",
            "River Sand",
            "Masonry Sand"
        };

        public bool IsActive()
        {
            return Status == "Active";
        }

        public decimal CalculateVatAmount(decimal quantity)
        {
            return (UnitPrice * quantity) * (VatRate / 100);
        }

        public decimal CalculateTotalWithVat(decimal quantity)
        {
            return (UnitPrice * quantity) * (1 + VatRate / 100);
        }
    }
}