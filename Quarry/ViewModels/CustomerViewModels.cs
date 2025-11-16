using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;
using QuarryManagementSystem.Models.Domain;

namespace QuarryManagementSystem.ViewModels
{
    public class CustomerListViewModel
    {
        public List<Customer> Customers { get; set; } = new();
        
        [Display(Name = "Search Term")]
        public string? SearchTerm { get; set; }
        
        [Display(Name = "State")]
        public string? SelectedState { get; set; }
        
        [Display(Name = "Status")]
        public string? SelectedStatus { get; set; }
        
        // Pagination
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; }
        public int TotalCount { get; set; }
        
        // Dropdown data
        public List<SelectListItem> States { get; set; } = new();
        public List<SelectListItem> Statuses { get; set; } = new();
        
        // Helper properties
        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;
        
        public string? ErrorMessage { get; set; }
    }

    public class CustomerCreateViewModel
    {
        [Required(ErrorMessage = "Company name is required")]
        [StringLength(100)]
        [Display(Name = "Company Name")]
        public string Name { get; set; } = string.Empty;

        [StringLength(20)]
        [Display(Name = "RC Number")]
        public string? RCNumber { get; set; }

        [Required(ErrorMessage = "Location is required")]
        [StringLength(255)]
        public string Location { get; set; } = string.Empty;

        [Required(ErrorMessage = "LGA is required")]
        [StringLength(100)]
        [Display(Name = "Local Government Area")]
        public string LGA { get; set; } = string.Empty;

        [Required(ErrorMessage = "State is required")]
        [StringLength(50)]
        public string State { get; set; } = string.Empty;

        [StringLength(50)]
        [Display(Name = "Mining License Number")]
        public string? MiningLicenseNumber { get; set; }

        [StringLength(100)]
        [Display(Name = "Contact Person")]
        public string? ContactPerson { get; set; }

        [Required(ErrorMessage = "Phone number is required")]
        [RegularExpression(@"^(\+234|0)[789][01]\d{9}$", ErrorMessage = "Invalid Nigerian phone number format")]
        [StringLength(20)]
        public string Phone { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Invalid email format")]
        [StringLength(100)]
        public string? Email { get; set; }

        [StringLength(20)]
        [Display(Name = "Tax Identification Number")]
        public string? TIN { get; set; }

        [StringLength(20)]
        [Display(Name = "Bank Verification Number")]
        public string? BVN { get; set; }

        [StringLength(500)]
        [Display(Name = "Billing Address")]
        public string? BillingAddress { get; set; }

        [Display(Name = "Credit Limit")]
        [Range(0, 999999999.99, ErrorMessage = "Credit limit must be between 0 and 999,999,999.99")]
        public decimal CreditLimit { get; set; } = 0;

        [StringLength(20)]
        public string Status { get; set; } = "Active";

        // Dropdown data
        public List<SelectListItem> States { get; set; } = new();
        public List<SelectListItem> LGAs { get; set; } = new();
        public List<SelectListItem> Statuses { get; set; } = new();
    }

    public class CustomerEditViewModel : CustomerCreateViewModel
    {
        public int Id { get; set; }

        [Display(Name = "Outstanding Balance")]
        [DataType(DataType.Currency)]
        public decimal OutstandingBalance { get; set; }

        [Display(Name = "Available Credit")]
        [DataType(DataType.Currency)]
        public decimal AvailableCredit { get; set; }
    }

    public class CustomerDetailsViewModel
    {
        public Customer Customer { get; set; } = new();
        
        public List<CustomerTransactionViewModel> RecentTransactions { get; set; } = new();
        public List<CustomerInvoiceViewModel> RecentInvoices { get; set; } = new();
        
        [Display(Name = "Total Transactions")]
        public int TotalTransactions { get; set; }
        
        [Display(Name = "Total Invoice Amount")]
        [DataType(DataType.Currency)]
        public decimal TotalInvoiceAmount { get; set; }
        
        [Display(Name = "Average Transaction Value")]
        [DataType(DataType.Currency)]
        public decimal AverageTransactionValue { get; set; }
        
        [Display(Name = "Last Transaction Date")]
        [DataType(DataType.Date)]
        public DateTime? LastTransactionDate { get; set; }
    }

    public class CustomerTransactionViewModel
    {
        public int Id { get; set; }
        
        [Display(Name = "Transaction Number")]
        public string TransactionNumber { get; set; } = string.Empty;
        
        [Display(Name = "Date")]
        [DataType(DataType.Date)]
        public DateTime TransactionDate { get; set; }
        
        [Display(Name = "Vehicle Registration")]
        public string VehicleRegNumber { get; set; } = string.Empty;
        
        [Display(Name = "Material")]
        public string MaterialName { get; set; } = string.Empty;
        
        [Display(Name = "Net Weight (kg)")]
        [DisplayFormat(DataFormatString = "{0:N0}")]
        public decimal NetWeight { get; set; }
        
        [Display(Name = "Amount")]
        [DataType(DataType.Currency)]
        public decimal TotalAmount { get; set; }
        
        [Display(Name = "Status")]
        public string Status { get; set; } = string.Empty;
    }

    public class CustomerInvoiceViewModel
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
        
        [Display(Name = "Total Amount")]
        [DataType(DataType.Currency)]
        public decimal TotalAmount { get; set; }
        
        [Display(Name = "Paid Amount")]
        [DataType(DataType.Currency)]
        public decimal PaidAmount { get; set; }
        
        [Display(Name = "Outstanding Balance")]
        [DataType(DataType.Currency)]
        public decimal OutstandingBalance { get; set; }
        
        [Display(Name = "Status")]
        public string Status { get; set; } = string.Empty;
        
        [Display(Name = "Payment Status")]
        public string PaymentStatus
        {
            get
            {
                return Status switch
                {
                    "Paid" => "Paid",
                    "Overdue" => "Overdue",
                    _ => DueDate.HasValue && DueDate.Value < DateTime.Now ? "Overdue" : "Unpaid"
                };
            }
        }
    }

    public class CustomerCreditCheckViewModel
    {
        public int CustomerId { get; set; }
        
        [Display(Name = "Customer Name")]
        public string CustomerName { get; set; } = string.Empty;
        
        [Display(Name = "Credit Limit")]
        [DataType(DataType.Currency)]
        public decimal CreditLimit { get; set; }
        
        [Display(Name = "Outstanding Balance")]
        [DataType(DataType.Currency)]
        public decimal OutstandingBalance { get; set; }
        
        [Display(Name = "Available Credit")]
        [DataType(DataType.Currency)]
        public decimal AvailableCredit { get; set; }
        
        [Display(Name = "Additional Amount")]
        [DataType(DataType.Currency)]
        public decimal AdditionalAmount { get; set; }
        
        [Display(Name = "Exceeds Credit Limit")]
        public bool ExceedsCreditLimit { get; set; }
        
        [Display(Name = "New Outstanding Balance")]
        [DataType(DataType.Currency)]
        public decimal NewOutstandingBalance { get; set; }
        
        [Display(Name = "Warning Message")]
        public string? WarningMessage { get; set; }
    }
}