using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;
using QuarryManagementSystem.Models.Domain;

namespace QuarryManagementSystem.ViewModels
{
    public class QuotationListViewModel
    {
        public List<Quotation> Quotations { get; set; } = new();

        [Display(Name = "Search Term")]
        public string? SearchTerm { get; set; }

        [Display(Name = "Status")]
        public string? SelectedStatus { get; set; }

        [Display(Name = "Date From")]
        [DataType(DataType.Date)]
        public DateTime? DateFrom { get; set; }

        [Display(Name = "Date To")]
        [DataType(DataType.Date)]
        public DateTime? DateTo { get; set; }

        // Pagination
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; }
        public int TotalCount { get; set; }

        // Dropdown data
        public List<SelectListItem> Statuses { get; set; } = new();

        // Helper properties
        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;

        public string? ErrorMessage { get; set; }

        // Summary statistics
        [Display(Name = "Draft")]
        public int DraftCount => Quotations.Count(q => q.Status == "Draft");

        [Display(Name = "Sent")]
        public int SentCount => Quotations.Count(q => q.Status == "Sent");

        [Display(Name = "Accepted")]
        public int AcceptedCount => Quotations.Count(q => q.Status == "Accepted");

        [Display(Name = "Rejected")]
        public int RejectedCount => Quotations.Count(q => q.Status == "Rejected");

        [Display(Name = "Expired")]
        public int ExpiredCount => Quotations.Count(q => q.Status == "Expired");

        [Display(Name = "Cancelled")]
        public int CancelledCount => Quotations.Count(q => q.Status == "Cancelled");
    }

    public class QuotationItemEditViewModel
    {
        public int? Id { get; set; }

        [Display(Name = "Material")]
        public int? MaterialId { get; set; }

        [StringLength(200)]
        [Display(Name = "Description")]
        public string? Description { get; set; }

        [Display(Name = "Quantity")]
        [Range(typeof(decimal), "0.00", "999999999.99")]
        public decimal Quantity { get; set; }

        [StringLength(20)]
        [Display(Name = "Unit")]
        public string Unit { get; set; } = "Ton";

        [Display(Name = "Unit Price")]
        [DataType(DataType.Currency)]
        [Range(typeof(decimal), "0.00", "999999999.99")]
        public decimal UnitPrice { get; set; }

        [Display(Name = "VAT Rate (%)")]
        [Range(typeof(decimal), "0.00", "100.00")]
        public decimal VatRate { get; set; } = 7.5m;

        [Display(Name = "Subtotal")]
        [DataType(DataType.Currency)]
        public decimal LineSubTotal => Math.Round(Quantity * UnitPrice, 2);

        [Display(Name = "VAT Amount")]
        [DataType(DataType.Currency)]
        public decimal LineVatAmount => Math.Round(LineSubTotal * (VatRate / 100m), 2);

        [Display(Name = "Total")]
        [DataType(DataType.Currency)]
        public decimal LineTotal => LineSubTotal + LineVatAmount;
    }

    public class QuotationCreateEditViewModel
    {
        public int? Id { get; set; }

        // Quotation Header
        [Display(Name = "Quotation Date")]
        [DataType(DataType.Date)]
        public DateTime QuotationDate { get; set; } = DateTime.Now;

        [Display(Name = "Expiry Date")]
        [DataType(DataType.Date)]
        public DateTime? ExpiryDate { get; set; }

        [Display(Name = "Customer")]
        public int? CustomerId { get; set; }

        [StringLength(500)]
        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        [Display(Name = "Status")]
        public string Status { get; set; } = "Draft"; // Draft, Sent, Accepted, Rejected, Cancelled, Expired

        // Items
        [Display(Name = "Items")]
        public List<QuotationItemEditViewModel> Items { get; set; } = new();

        // Calculated Fields
        [Display(Name = "Subtotal")]
        [DataType(DataType.Currency)]
        public decimal SubTotal => Math.Round(Items.Sum(i => i.LineSubTotal), 2);

        [Display(Name = "VAT Amount")]
        [DataType(DataType.Currency)]
        public decimal VatAmount => Math.Round(Items.Sum(i => i.LineVatAmount), 2);

        [Display(Name = "Total Amount")]
        [DataType(DataType.Currency)]
        public decimal TotalAmount => SubTotal + VatAmount;

        // Dropdown data
        public List<SelectListItem> Customers { get; set; } = new();
        public List<SelectListItem> Materials { get; set; } = new();

        // Helper
        public bool HasAtLeastOneItem => Items != null && Items.Any(i => i.Quantity > 0 && (i.UnitPrice > 0 || i.MaterialId.HasValue));
    }

    public class QuotationDetailsViewModel
    {
        public Quotation Quotation { get; set; } = new();
        public List<QuotationItem> Items { get; set; } = new();

        [Display(Name = "Amount in Words")]
        public string AmountInWords { get; set; } = string.Empty;

        [Display(Name = "Is Expired")]
        public bool IsExpired => Quotation.IsExpired();
    }

    public class QuotationPrintViewModel
    {
        public Quotation Quotation { get; set; } = new();
        public List<QuotationItem> Items { get; set; } = new();

        [Display(Name = "Amount in Words")]
        public string AmountInWords { get; set; } = string.Empty;

        public CompanyDetailsViewModel CompanyDetails { get; set; } = new();

        [Display(Name = "Print Date")]
        [DataType(DataType.DateTime)]
        public DateTime PrintDate { get; set; } = DateTime.Now;
    }
}