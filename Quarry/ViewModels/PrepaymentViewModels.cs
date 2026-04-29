using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using QuarryManagementSystem.Models.Domain;

namespace QuarryManagementSystem.ViewModels
{
    /// <summary>
    /// Root view model for the Create/Edit Prepayment form. Wraps the prepayment
    /// header plus a dynamic list of line items (each a material + qty + unit price).
    /// </summary>
    public class PrepaymentCreateEditViewModel
    {
        public int Id { get; set; }

        public string? PrepaymentNumber { get; set; }

        [Required(ErrorMessage = "Please select a customer.")]
        [Display(Name = "Customer")]
        public int? CustomerId { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Prepayment Date")]
        public DateTime PrepaymentDate { get; set; } = DateTime.Now;

        [DataType(DataType.Date)]
        [Display(Name = "Expected Pickup Date")]
        public DateTime? ExpectedPickupDate { get; set; }

        [StringLength(50)]
        [Display(Name = "Payment Method")]
        public string PaymentMethod { get; set; } = string.Empty;

        /// <summary>
        /// FK selected from the PaymentMethods dropdown. Required on new
        /// prepayments; the legacy <see cref="PaymentMethod"/> string is kept
        /// denormalized at save time so old reports / joins still work.
        /// </summary>
        [Display(Name = "Payment Method")]
        public int? PaymentMethodId { get; set; }

        [StringLength(100)]
        [Display(Name = "Reference")]
        public string? Reference { get; set; }

        [StringLength(500)]
        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        [StringLength(20)]
        [Display(Name = "Status")]
        public string Status { get; set; } = "Active";

        public List<PrepaymentLineItemInput> Items { get; set; } = new();

        // Display helpers (not persisted)
        public decimal TotalAmount { get; set; }

        // Dropdown sources
        public List<SelectListItem> Customers { get; set; } = new();
        public List<SelectListItem> Materials { get; set; } = new();
        public List<SelectListItem> PaymentMethods { get; set; } = new();
    }

    /// <summary>
    /// One editable line item row inside the form.
    /// </summary>
    public class PrepaymentLineItemInput
    {
        public int Id { get; set; }

        [Display(Name = "Material")]
        public int? MaterialId { get; set; }

        [Display(Name = "Quantity")]
        public decimal Quantity { get; set; }

        [StringLength(20)]
        [Display(Name = "Unit")]
        public string Unit { get; set; } = "Ton";

        [Display(Name = "Unit Price")]
        public decimal UnitPrice { get; set; }

        [Display(Name = "VAT Amount")]
        public decimal VatAmount { get; set; }

        [Display(Name = "Rebate Amount")]
        public decimal RebateAmount { get; set; }

        [Display(Name = "Line Total")]
        public decimal LineTotal { get; set; }

        // Usage info (only populated when editing an existing prepayment)
        public decimal UsedAmount { get; set; }
        public decimal UsedQuantity { get; set; }
    }

    /// <summary>
    /// Details view model that adds aggregated usage / application information
    /// for display on the Details screen.
    /// </summary>
    public class PrepaymentDetailsViewModel
    {
        public CustomerPrepayment Prepayment { get; set; } = null!;
        public List<PrepaymentLineItem> LineItems { get; set; } = new();
        public List<PrepaymentApplication> Applications { get; set; } = new();
        public decimal TotalApplied { get; set; }
        public decimal TotalRemaining { get; set; }
    }

    /// <summary>
    /// Print view model used by the Receipt.cshtml printable view.
    /// Structurally mirrors InvoicePrintViewModel so the Razor can look almost identical.
    /// </summary>
    public class PrepaymentReceiptPrintViewModel
    {
        public CustomerPrepayment Prepayment { get; set; } = null!;
        public List<PrepaymentLineItem> LineItems { get; set; } = new();
        public string AmountInWords { get; set; } = string.Empty;
        public CompanyDetailsViewModel CompanyDetails { get; set; } = new();
        public DateTime PrintDate { get; set; } = DateTime.Now;
    }
}
