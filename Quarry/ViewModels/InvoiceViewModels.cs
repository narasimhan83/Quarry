using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;
using QuarryManagementSystem.Models.Domain;

namespace QuarryManagementSystem.ViewModels
{
    public class InvoiceListViewModel
    {
        public List<Invoice> Invoices { get; set; } = new();
        
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
        [Display(Name = "Total Outstanding")]
        [DataType(DataType.Currency)]
        public decimal TotalOutstanding => Invoices.Where(i => i.Status == "Unpaid" || i.Status == "Partial").Sum(i => i.OutstandingBalance);
        
        [Display(Name = "Total Overdue")]
        [DataType(DataType.Currency)]
        public decimal TotalOverdue => Invoices.Where(i => i.Status == "Overdue").Sum(i => i.OutstandingBalance);

        [Display(Name = "Unpaid Invoices Count")]
        public int UnpaidInvoices => Invoices.Count(i => i.Status == "Unpaid" || i.Status == "Partial");

        [Display(Name = "Overdue Invoices Count")]
        public int OverdueInvoices => Invoices.Count(i => i.Status == "Overdue");
    }

    public class InvoiceCreateViewModel
    {
        // Invoice Header
        [Display(Name = "Invoice Date")]
        [DataType(DataType.Date)]
        public DateTime InvoiceDate { get; set; } = DateTime.Now;

        [Display(Name = "Due Date")]
        [DataType(DataType.Date)]
        public DateTime? DueDate { get; set; }

        [Display(Name = "Customer")]
        public int? CustomerId { get; set; }

        [Display(Name = "Selected Payment Terms")]
        public string? SelectedPaymentTerms { get; set; } = "30 days";

        [StringLength(50)]
        [Display(Name = "LGA Receipt Number")]
        public string? LGAReceiptNumber { get; set; }

        [StringLength(500)]
        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        // VAT Information
        [Display(Name = "VAT Rate (%)")]
        public decimal VatRate { get; set; } = 7.5m;

        // Weighment Selection
        [Display(Name = "Selected Weighments")]
        public int[] SelectedWeighmentIds { get; set; } = Array.Empty<int>();

        // Calculated Fields
        [Display(Name = "Subtotal")]
        [DataType(DataType.Currency)]
        public decimal SubTotal { get; set; }

        [Display(Name = "VAT Amount")]
        [DataType(DataType.Currency)]
        public decimal VatAmount { get; set; }

        [Display(Name = "Total Amount")]
        [DataType(DataType.Currency)]
        public decimal TotalAmount { get; set; }

        // Dropdown data
        public List<SelectListItem> Customers { get; set; } = new();
        public List<SelectListItem> PaymentTermsList { get; set; } = new();
        public List<WeighmentSelectionViewModel> AvailableWeighments { get; set; } = new();
    }

    public class InvoiceEditViewModel
    {
        public int Id { get; set; }

        [Display(Name = "Invoice Number")]
        public string InvoiceNumber { get; set; } = string.Empty;

        [Display(Name = "Invoice Date")]
        [DataType(DataType.Date)]
        public DateTime InvoiceDate { get; set; }

        [Display(Name = "Due Date")]
        [DataType(DataType.Date)]
        public DateTime? DueDate { get; set; }

        [Display(Name = "Payment Terms")]
        public string? PaymentTerms { get; set; }

        [StringLength(50)]
        [Display(Name = "LGA Receipt Number")]
        public string? LGAReceiptNumber { get; set; }

        [StringLength(500)]
        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        [Display(Name = "Status")]
        public string Status { get; set; } = string.Empty;

        [Display(Name = "Created By")]
        public string? CreatedBy { get; set; }

        [Display(Name = "Created Date")]
        [DataType(DataType.DateTime)]
        public DateTime CreatedAt { get; set; }
    }

    public class InvoiceDetailsViewModel
    {
        public Invoice Invoice { get; set; } = new();

        [Display(Name = "Amount in Words")]
        public string AmountInWords { get; set; } = string.Empty;

        [Display(Name = "Can Edit Payment")]
        public bool CanEditPayment { get; set; }

        [Display(Name = "Can Cancel")]
        public bool CanCancel { get; set; }

        [Display(Name = "Overdue Days")]
        public int? OverdueDays => Invoice.GetOverdueDays()?.Days;

        [Display(Name = "Payment Status")]
        public string PaymentStatus => Invoice.IsPaid() ? "Paid" : Invoice.IsOverdue() ? "Overdue" : "Outstanding";
    }

    public class InvoicePrintViewModel
    {
        public Invoice Invoice { get; set; } = new();

        [Display(Name = "Amount in Words")]
        public string AmountInWords { get; set; } = string.Empty;

        public CompanyDetailsViewModel CompanyDetails { get; set; } = new();

        public List<InvoiceItemViewModel> InvoiceItems { get; set; } = new();

        [Display(Name = "Print Date")]
        [DataType(DataType.DateTime)]
        public DateTime PrintDate { get; set; } = DateTime.Now;
    }

    public class InvoiceItemViewModel
    {
        [Display(Name = "Description")]
        public string Description { get; set; } = string.Empty;

        [Display(Name = "Quantity")]
        public decimal Quantity { get; set; }

        [Display(Name = "Unit")]
        public string Unit { get; set; } = string.Empty;

        [Display(Name = "Unit Price")]
        [DataType(DataType.Currency)]
        public decimal UnitPrice { get; set; }

        [Display(Name = "Total Amount")]
        [DataType(DataType.Currency)]
        public decimal TotalAmount { get; set; }

        [Display(Name = "VAT Amount")]
        [DataType(DataType.Currency)]
        public decimal VatAmount { get; set; }

        [Display(Name = "Total with VAT")]
        [DataType(DataType.Currency)]
        public decimal TotalWithVat => TotalAmount + VatAmount;
    }

    public class InvoicePaymentViewModel
    {
        public int InvoiceId { get; set; }

        [Display(Name = "Invoice Number")]
        public string InvoiceNumber { get; set; } = string.Empty;

        [Display(Name = "Customer")]
        public string CustomerName { get; set; } = string.Empty;

        [Display(Name = "Total Amount")]
        [DataType(DataType.Currency)]
        public decimal TotalAmount { get; set; }

        [Display(Name = "Outstanding Amount")]
        [DataType(DataType.Currency)]
        public decimal OutstandingAmount { get; set; }

        [Required(ErrorMessage = "Payment amount is required")]
        [Range(0.01, 999999999.99, ErrorMessage = "Payment amount must be between 0.01 and 999,999,999.99")]
        [Display(Name = "Payment Amount")]
        [DataType(DataType.Currency)]
        public decimal PaymentAmount { get; set; }

        [Required(ErrorMessage = "Payment date is required")]
        [Display(Name = "Payment Date")]
        [DataType(DataType.Date)]
        public DateTime PaymentDate { get; set; } = DateTime.Now;

        [Required(ErrorMessage = "Payment method is required")]
        [StringLength(50)]
        [Display(Name = "Payment Method")]
        public string PaymentMethod { get; set; } = string.Empty;

        [StringLength(500)]
        [Display(Name = "Payment Notes")]
        public string? PaymentNotes { get; set; }

        // Dropdown data
        public List<SelectListItem> PaymentMethods { get; set; } = new();
    }

    public class WeighmentSelectionViewModel
    {
        public int Id { get; set; }

        [Display(Name = "Transaction Number")]
        public string TransactionNumber { get; set; } = string.Empty;

        [Display(Name = "Date")]
        [DataType(DataType.Date)]
        public DateTime TransactionDate { get; set; }

        [Display(Name = "Vehicle")]
        public string VehicleRegNumber { get; set; } = string.Empty;

        [Display(Name = "Material")]
        public string MaterialName { get; set; } = string.Empty;

        [Display(Name = "Net Weight (kg)")]
        [DisplayFormat(DataFormatString = "{0:N0}")]
        public decimal NetWeight { get; set; }

        [Display(Name = "Amount")]
        [DataType(DataType.Currency)]
        public decimal TotalAmount { get; set; }

        [Display(Name = "Selected")]
        public bool IsSelected { get; set; }
    }

    public class CompanyDetailsViewModel
    {
        [Display(Name = "Company Name")]
        public string CompanyName { get; set; } = "Nigerian Quarry Management System";

        [Display(Name = "Address")]
        public string Address { get; set; } = string.Empty;

        [Display(Name = "City")]
        public string City { get; set; } = string.Empty;

        [Display(Name = "State")]
        public string State { get; set; } = string.Empty;

        [Display(Name = "Phone")]
        public string Phone { get; set; } = string.Empty;

        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Website")]
        public string Website { get; set; } = string.Empty;

        [Display(Name = "Tax Number")]
        public string TaxNumber { get; set; } = string.Empty;

        [Display(Name = "Bank Details")]
        public string BankDetails { get; set; } = string.Empty;
    }

    public class InvoiceSummaryViewModel
    {
        [Display(Name = "Total Invoices")]
        public int TotalInvoices { get; set; }

        [Display(Name = "Unpaid Invoices")]
        public int UnpaidInvoices { get; set; }

        [Display(Name = "Paid Invoices")]
        public int PaidInvoices { get; set; }

        [Display(Name = "Overdue Invoices")]
        public int OverdueInvoices { get; set; }

        [Display(Name = "Total Revenue")]
        [DataType(DataType.Currency)]
        public decimal TotalRevenue { get; set; }

        [Display(Name = "Outstanding Amount")]
        [DataType(DataType.Currency)]
        public decimal OutstandingAmount { get; set; }

        [Display(Name = "Average Invoice Value")]
        [DataType(DataType.Currency)]
        public decimal AverageInvoiceValue => TotalInvoices > 0 ? TotalRevenue / TotalInvoices : 0;

        [Display(Name = "Collection Rate")]
        [DisplayFormat(DataFormatString = "{0:P1}")]
        public double CollectionRate => TotalRevenue > 0 ? (double)((TotalRevenue - OutstandingAmount) / TotalRevenue) * 100 : 0;
    }

    public class InvoiceFilterViewModel
    {
        [Display(Name = "Customer")]
        public int? CustomerId { get; set; }

        [Display(Name = "Status")]
        public string? Status { get; set; }

        [Display(Name = "Date From")]
        [DataType(DataType.Date)]
        public DateTime? DateFrom { get; set; }

        [Display(Name = "Date To")]
        [DataType(DataType.Date)]
        public DateTime? DateTo { get; set; }

        [Display(Name = "Amount Range From")]
        [DataType(DataType.Currency)]
        public decimal? AmountRangeFrom { get; set; }

        [Display(Name = "Amount Range To")]
        [DataType(DataType.Currency)]
        public decimal? AmountRangeTo { get; set; }

        // Dropdown data
        public List<SelectListItem> Customers { get; set; } = new();
        public List<SelectListItem> Statuses { get; set; } = new();
    }
}