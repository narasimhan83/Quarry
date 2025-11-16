using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using QuarryManagementSystem.Models.Domain;
using QuarryManagementSystem.Utils;

namespace QuarryManagementSystem.ViewModels
{
    public class WeighmentListViewModel
    {
        public List<WeighmentTransaction> Weighments { get; set; } = new();
        
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
    }

    public class WeighmentCreateViewModel
    {
        // Basic Information
        [Display(Name = "Transaction Date")]
        [DataType(DataType.DateTime)]
        public DateTime TransactionDate { get; set; } = DateTime.Now;

        [Display(Name = "Transaction Number")]
        public string TransactionNumber { get; set; } = string.Empty;

        // Vehicle Information
        [Required(ErrorMessage = "Vehicle registration is required")]
        [StringLength(20)]
        [RegularExpression(@"^[A-Z]{3}-\d{3}-[A-Z]{3}$|^[A-Z]{2}\d{2}[A-Z]{2}\d{3}$", ErrorMessage = "Invalid Nigerian vehicle registration format")]
        [Display(Name = "Vehicle Registration")]
        public string VehicleRegNumber { get; set; } = string.Empty;

        [StringLength(100)]
        [Display(Name = "Driver Name")]
        public string? DriverName { get; set; }

        [StringLength(20)]
        [RegularExpression(@"^(\+234|0)[789][01]\d{9}$", ErrorMessage = "Invalid Nigerian phone number")]
        [Display(Name = "Driver Phone")]
        public string? DriverPhone { get; set; }

        // Selection Fields
        [Display(Name = "Customer")]
        public int? CustomerId { get; set; }

        [Display(Name = "Weighbridge")]
        public int? WeighbridgeId { get; set; }

        [Display(Name = "Material")]
        public int? MaterialId { get; set; }

        // Pricing
        [Display(Name = "Price Per Unit")]
        [Range(0.01, 999999.99, ErrorMessage = "Price per unit must be between 0.01 and 999,999.99")]
        public decimal? PricePerUnit { get; set; }

        [Display(Name = "VAT Rate (%)")]
        [Range(0, 100, ErrorMessage = "VAT rate must be between 0 and 100")]
        public decimal VatRate { get; set; } = 7.5m;

        // Weight Information
        [Required(ErrorMessage = "Gross weight is required")]
        [Display(Name = "Gross Weight (kg)")]
        [Range(0.01, 999999.99, ErrorMessage = "Gross weight must be between 0.01 and 999,999.99")]
        public decimal GrossWeight { get; set; }

        [Display(Name = "Tare Weight (kg)")]
        [Range(0, 999999.99, ErrorMessage = "Tare weight must be between 0 and 999,999.99")]
        public decimal? TareWeight { get; set; }

        [NotMapped]
        [Display(Name = "Net Weight (kg)")]
        public decimal NetWeight => GrossWeight - (TareWeight ?? 0);

        [Required]
        [StringLength(10)]
        [Display(Name = "Weight Unit")]
        public string WeightUnit { get; set; } = "kg";

        // Timing
        [Display(Name = "Entry Time")]
        [DataType(DataType.DateTime)]
        public DateTime? EntryTime { get; set; }

        [Display(Name = "Exit Time")]
        [DataType(DataType.DateTime)]
        public DateTime? ExitTime { get; set; }

        // Additional Information
        [StringLength(20)]
        [Display(Name = "Transaction Type")]
        public string TransactionType { get; set; } = "Sales";

        [StringLength(20)]
        public string Status { get; set; } = "InProgress";

        [StringLength(50)]
        [Display(Name = "Challan Number")]
        public string? ChallanNumber { get; set; }

        // Dropdown data
        public List<SelectListItem> Customers { get; set; } = new();
        public List<SelectListItem> Materials { get; set; } = new();
        public List<SelectListItem> Weighbridges { get; set; } = new();
        public List<SelectListItem> TransactionTypes { get; set; } = new();
        public List<SelectListItem> Statuses { get; set; } = new();

        // Calculated properties
        [Display(Name = "Subtotal")]
        [DataType(DataType.Currency)]
        public decimal? SubTotal { get; set; }

        [Display(Name = "VAT Amount")]
        [DataType(DataType.Currency)]
        public decimal? VatAmount { get; set; }

        [Display(Name = "Total Amount")]
        [DataType(DataType.Currency)]
        public decimal? TotalAmount { get; set; }

        // Helper method to calculate financials
        public void CalculateFinancials()
        {
            if (NetWeight > 0 && PricePerUnit.HasValue)
            {
                decimal quantityInTons = WeightUnit == "kg" ? NetWeight / 1000 : NetWeight;
                SubTotal = quantityInTons * PricePerUnit.Value;
                VatAmount = SubTotal * (VatRate / 100);
                TotalAmount = SubTotal + VatAmount;
            }
        }
    }

    public class WeighmentEditViewModel : WeighmentCreateViewModel
    {
        public int Id { get; set; }

        [Display(Name = "Modified By")]
        public string? ModifiedBy { get; set; }

        [Display(Name = "Modified Date")]
        [DataType(DataType.DateTime)]
        public DateTime? ModifiedAt { get; set; }
    }

    public class WeighmentOperationsViewModel
    {
        public List<WeighmentTransaction> ActiveWeighments { get; set; } = new();
        public List<WeighmentTransaction> CompletedToday { get; set; } = new();
        public List<Weighbridge> ActiveWeighbridges { get; set; } = new();
        
        public string? ErrorMessage { get; set; }
        
        [Display(Name = "Total Active")]
        public int TotalActive => ActiveWeighments.Count;
        
        [Display(Name = "Completed Today")]
        public int CompletedTodayCount => CompletedToday.Count;
        
        [Display(Name = "Total Revenue Today")]
        [DataType(DataType.Currency)]
        public decimal TotalRevenueToday => CompletedToday.Sum(w => w.TotalAmount ?? 0);
    }

    public class WeighmentDetailsViewModel
    {
        public WeighmentTransaction Weighment { get; set; } = new();
        
        [Display(Name = "Duration")]
        public TimeSpan? Duration => Weighment.GetDuration();
        
        [Display(Name = "Amount in Words")]
        public string AmountInWords => Weighment.TotalAmount.HasValue ?
            NumberToWordsConverter.ConvertAmountToWords(Weighment.TotalAmount.Value) : string.Empty;
        
        [Display(Name = "Can Edit")]
        public bool CanEdit => Weighment.Status != "Completed" && Weighment.Status != "Invoiced";
        
        [Display(Name = "Can Delete")]
        public bool CanDelete => Weighment.Status != "Completed" && Weighment.Status != "Invoiced";
        
        [Display(Name = "Can Invoice")]
        public bool CanInvoice => Weighment.Status == "Completed" && !Weighment.IsInvoiced;
    }

    public class WeighmentSummaryViewModel
    {
        [Display(Name = "Total Transactions")]
        public int TotalTransactions { get; set; }
        
        [Display(Name = "Total Weight (kg)")]
        [DisplayFormat(DataFormatString = "{0:N0}")]
        public decimal TotalWeight { get; set; }
        
        [Display(Name = "Total Revenue")]
        [DataType(DataType.Currency)]
        public decimal TotalRevenue { get; set; }
        
        [Display(Name = "Average Transaction Value")]
        [DataType(DataType.Currency)]
        public decimal AverageTransactionValue => TotalTransactions > 0 ? TotalRevenue / TotalTransactions : 0;
        
        [Display(Name = "Active Transactions")]
        public int ActiveTransactions { get; set; }
        
        [Display(Name = "Completed Transactions")]
        public int CompletedTransactions { get; set; }
        
        [Display(Name = "Cancelled Transactions")]
        public int CancelledTransactions { get; set; }
    }

    public class WeighmentFilterViewModel
    {
        [Display(Name = "Customer")]
        public int? CustomerId { get; set; }
        
        [Display(Name = "Material")]
        public int? MaterialId { get; set; }
        
        [Display(Name = "Weighbridge")]
        public int? WeighbridgeId { get; set; }
        
        [Display(Name = "Date From")]
        [DataType(DataType.Date)]
        public DateTime? DateFrom { get; set; }
        
        [Display(Name = "Date To")]
        [DataType(DataType.Date)]
        public DateTime? DateTo { get; set; }
        
        [Display(Name = "Status")]
        public string? Status { get; set; }
        
        [Display(Name = "Vehicle Registration")]
        public string? VehicleRegNumber { get; set; }
        
        // Dropdown data
        public List<SelectListItem> Customers { get; set; } = new();
        public List<SelectListItem> Materials { get; set; } = new();
        public List<SelectListItem> Weighbridges { get; set; } = new();
        public List<SelectListItem> Statuses { get; set; } = new();
    }
}