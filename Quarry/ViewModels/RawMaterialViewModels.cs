using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace QuarryManagementSystem.ViewModels
{
    /// <summary>
    /// Used by both Create and Edit on RawMaterialController. Mirrors the
    /// entity 1:1 because the catalogue is intentionally small in Phase 2.
    /// </summary>
    public class RawMaterialEditViewModel
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "Raw Material Name")]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        [Display(Name = "Unit")]
        public string Unit { get; set; } = "Ton";

        [Required]
        [StringLength(20)]
        [Display(Name = "Status")]
        public string Status { get; set; } = "Active";
    }

    /// <summary>
    /// Used by RawMaterialReceiptController on Create and Edit. The receipt
    /// number is auto-generated when blank — operators rarely want to type
    /// these manually but we don't enforce it because some operations import
    /// receipts from external systems with their own numbering.
    /// </summary>
    public class RawMaterialReceiptEditViewModel
    {
        public int Id { get; set; }

        [StringLength(50)]
        [Display(Name = "Receipt Number")]
        public string? ReceiptNumber { get; set; }

        [Required(ErrorMessage = "Please select the quarry that received this material.")]
        [Display(Name = "Quarry")]
        public int? QuarryId { get; set; }

        [Required(ErrorMessage = "Please select a raw material.")]
        [Display(Name = "Raw Material")]
        public int? RawMaterialId { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Receipt Date")]
        public DateTime ReceiptDate { get; set; } = DateTime.Today;

        [Required]
        [Range(0.001, 9999999.999, ErrorMessage = "Quantity must be greater than zero.")]
        [Display(Name = "Quantity")]
        public decimal Quantity { get; set; }

        [Required]
        [Range(0, 999999.99, ErrorMessage = "Unit cost cannot be negative.")]
        [Display(Name = "Unit Cost")]
        public decimal UnitCost { get; set; }

        [StringLength(200)]
        [Display(Name = "Source / Supplier")]
        public string? Source { get; set; }

        [StringLength(500)]
        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        // Dropdown sources, populated by the controller before render.
        public List<SelectListItem> Quarries { get; set; } = new();
        public List<SelectListItem> RawMaterials { get; set; } = new();
    }
}
