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
        [Display(Name = "Customer Number")]
        public string? RCNumber { get; set; }

        [StringLength(255)]
        public string? Location { get; set; }

        [StringLength(100)]
        [Display(Name = "Local Government Area")]
        public string? LGA { get; set; }

        [StringLength(50)]
        public string? State { get; set; }

        [StringLength(50)]
        [Display(Name = "Mining License Number")]
        public string? MiningLicenseNumber { get; set; }

        [StringLength(100)]
        [Display(Name = "Contact Person")]
        public string? ContactPerson { get; set; }

        [RegularExpression(@"^(?:\+234|0)[7-9]\d{9}$", ErrorMessage = "Invalid Nigerian phone number format. Use +234XXXXXXXXXX or 0XXXXXXXXXX")]
        [StringLength(20)]
        public string? Phone { get; set; }

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

        // ---------- Classification ----------
        [Display(Name = "Customer Type")]
        public int? CustomerTypeId { get; set; }

        [Display(Name = "VAT Type")]
        public int? VatTypeId { get; set; }

        // ---------- Rebate ----------
        [Display(Name = "Has Rebate")]
        public bool HasRebate { get; set; } = false;

        [Display(Name = "Rebate Amount")]
        [Range(0, 999999999.99)]
        public decimal? RebateAmount { get; set; }

        // ---------- Transport ----------
        [Display(Name = "Transport Required")]
        public bool TransportRequired { get; set; } = false;

        [Display(Name = "Transport Amount")]
        [Range(0, 999999999.99)]
        public decimal? TransportAmount { get; set; }

        // ---------- Per-customer pricing (line items) ----------
        public List<CustomerMaterialPriceInput> MaterialPrices { get; set; } = new();

        // ---------- Per-customer trucks ----------
        public List<CustomerTruckInput> Trucks { get; set; } = new();

        // ---------- Per-customer bank accounts ----------
        public List<CustomerBankInput> BankAccounts { get; set; } = new();

        // Dropdown data
        public List<SelectListItem> States { get; set; } = new();
        public List<SelectListItem> LGAs { get; set; } = new();
        public List<SelectListItem> Statuses { get; set; } = new();
        public List<SelectListItem> CustomerTypes { get; set; } = new();
        public List<SelectListItem> VatTypes { get; set; } = new();
        public List<SelectListItem> Materials { get; set; } = new();
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

    /// <summary>
    /// One row in the per-customer material pricing table on Create/Edit.
    /// Only new or updated rows need to be submitted; the controller compares
    /// against the existing current price and adds a new history row when the
    /// price changes.
    /// </summary>
    public class CustomerMaterialPriceInput
    {
        public int Id { get; set; } // 0 = new row
        public int? MaterialId { get; set; }

        [Display(Name = "Unit Price")]
        public decimal UnitPrice { get; set; }

        [Display(Name = "VAT Rate (%)")]
        public decimal? VatRate { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Effective From")]
        public DateTime EffectiveFrom { get; set; } = DateTime.Today;

        [StringLength(200)]
        public string? Notes { get; set; }
    }

    /// <summary>
    /// One row in the per-customer truck list on Create/Edit.
    /// New rows arrive with Id = 0; existing ones round-trip their primary key.
    /// </summary>
    public class CustomerTruckInput
    {
        public int Id { get; set; } // 0 = new row

        [Required(ErrorMessage = "Truck number is required")]
        [StringLength(100)]
        [Display(Name = "Truck Number")]
        public string CustomerTruckNumber { get; set; } = string.Empty;

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;
    }

    /// <summary>
    /// One row in the per-customer bank-account list on Create/Edit.
    /// New rows arrive with Id = 0; existing ones round-trip their primary key.
    /// AccountNumber and BankName are required; the rest are optional.
    /// </summary>
    public class CustomerBankInput
    {
        public int Id { get; set; } // 0 = new row

        [Required(ErrorMessage = "Account number is required")]
        [StringLength(50)]
        [Display(Name = "Account Number")]
        public string AccountNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Bank name is required")]
        [StringLength(150)]
        [Display(Name = "Bank Name")]
        public string BankName { get; set; } = string.Empty;

        [StringLength(255)]
        [Display(Name = "Bank Address")]
        public string? BankAddress { get; set; }

        [StringLength(150)]
        [Display(Name = "Bank Branch")]
        public string? BankBranch { get; set; }

        [StringLength(20)]
        [Display(Name = "SWIFT Code")]
        public string? BankSwiftCode { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;
    }

    public class CustomerDetailsViewModel
    {
        public Customer Customer { get; set; } = new();

        public List<CustomerTransactionViewModel> RecentTransactions { get; set; } = new();
        public List<CustomerInvoiceViewModel> RecentInvoices { get; set; } = new();

        /// <summary>Current per-customer material prices (latest effective per pair).</summary>
        public List<CustomerMaterialPriceDisplay> CurrentPrices { get; set; } = new();

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

    public class CustomerMaterialPriceDisplay
    {
        public int MaterialId { get; set; }
        public string MaterialName { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public decimal? VatRate { get; set; }
        public DateTime EffectiveFrom { get; set; }
        public int HistoryCount { get; set; }
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
