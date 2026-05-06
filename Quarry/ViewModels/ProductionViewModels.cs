using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using QuarryManagementSystem.Models.Domain;

namespace QuarryManagementSystem.ViewModels
{
    /// <summary>
    /// Used by Production Create and Edit. Carries the run header plus the
    /// editable list of output lines. Status is intentionally not editable
    /// in the form — it transitions only via the Post / Cancel actions.
    /// </summary>
    public class ProductionRunEditViewModel
    {
        public int Id { get; set; }

        [StringLength(50)]
        [Display(Name = "Run Number")]
        public string? RunNumber { get; set; }

        [Required(ErrorMessage = "Please select a quarry.")]
        [Display(Name = "Quarry")]
        public int? QuarryId { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Run Date")]
        public DateTime RunDate { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "Please select the raw material consumed.")]
        [Display(Name = "Raw Material")]
        public int? RawMaterialId { get; set; }

        [Required]
        [Range(0.001, 9999999.999, ErrorMessage = "Input quantity must be greater than zero.")]
        [Display(Name = "Input Quantity")]
        public decimal InputQuantity { get; set; }

        [Range(0, 9999999.999, ErrorMessage = "Waste cannot be negative.")]
        [Display(Name = "Waste Quantity")]
        public decimal WasteQuantity { get; set; }

        [StringLength(200)]
        [Display(Name = "Operator")]
        public string? Operator { get; set; }

        [StringLength(500)]
        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        [Display(Name = "Status")]
        public string Status { get; set; } = "Draft";

        public List<ProductionOutputInput> Outputs { get; set; } = new();

        // Dropdown sources, populated by the controller.
        public List<SelectListItem> Quarries { get; set; } = new();
        public List<SelectListItem> RawMaterials { get; set; } = new();
        public List<SelectListItem> Materials { get; set; } = new();
    }

    /// <summary>
    /// One editable output line on the production run form. AllocatedCost is
    /// computed at Post-time by InventoryService — operators don't enter it.
    /// </summary>
    public class ProductionOutputInput
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Material is required.")]
        [Display(Name = "Material")]
        public int? MaterialId { get; set; }

        [Required]
        [Range(0.001, 9999999.999, ErrorMessage = "Output quantity must be positive.")]
        [Display(Name = "Quantity")]
        public decimal Quantity { get; set; }

        // Read-only display helper, populated on Edit when the run is Posted.
        public decimal AllocatedCost { get; set; }
    }

    /// <summary>
    /// Read-only Details model. Carries the entity directly plus a few
    /// computed convenience values for the view.
    /// </summary>
    public class ProductionRunDetailsViewModel
    {
        public ProductionRun Run { get; set; } = null!;

        public decimal TotalOutputQuantity => Run.Outputs?.Sum(o => o.Quantity) ?? 0m;

        public decimal MassBalanceDelta => Run.InputQuantity - TotalOutputQuantity - Run.WasteQuantity;

        public decimal TotalAllocatedCost => Run.Outputs?.Sum(o => o.AllocatedCost) ?? 0m;
    }
}
