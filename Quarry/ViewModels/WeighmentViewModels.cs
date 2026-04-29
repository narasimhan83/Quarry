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
        [RegularExpression(@"^(?:\+234|0)[\s-]?[7-9]\d{2}[\s-]?\d{3}[\s-]?\d{4}$", ErrorMessage = "Invalid Nigerian phone number")]
        [Display(Name = "Driver Phone")]
        public string? DriverPhone { get; set; }

        // Selection Fields
        [Display(Name = "Customer")]
        public int? CustomerId { get; set; }

        [Display(Name = "Weighbridge")]
        public int? WeighbridgeId { get; set; }

        [Display(Name = "Material")]
        public int? MaterialId { get; set; }

        // Prepayment allocation hint (optional). When set, the operator picked a
        // specific active prepayment for this weighment and the later invoice
        // should drain that prepayment first before falling back to FIFO.
        [Display(Name = "Prepayment")]
        public int? SelectedPrepaymentId { get; set; }

        [Display(Name = "Prepayment Line Item")]
        public int? SelectedPrepaymentLineItemId { get; set; }

        // Pricing
        [Display(Name = "Price Per Unit")]
        [Range(0.01, 999999.99, ErrorMessage = "Price per unit must be between 0.01 and 999,999.99")]
        public decimal? PricePerUnit { get; set; }

        [Display(Name = "VAT Rate (%)")]
        [Range(0, 100, ErrorMessage = "VAT rate must be between 0 and 100")]
        public decimal VatRate { get; set; } = 7.5m;

        // Weight Information
        //
        // Real quarry dispatch flow:
        //   1. Empty truck enters → weighed = Tare weight
        //   2. Truck loaded with material → weighed again = Gross (total) weight
        //   3. Net weight = Gross − Tare (the material actually loaded, what we bill)
        //
        // Both Tare and Gross come from the weighbridge. Net is derived and is
        // the billable quantity used in CalculateFinancials.
        [Display(Name = "Tare Weight (tons)")]
        [Range(0, 9999.999, ErrorMessage = "Tare weight must be between 0 and 9,999.999 tons")]
        public decimal? TareWeight { get; set; }

        // Gross weight is captured later — typically only after the truck is
        // loaded and returns to the weighbridge for the second weighing. The
        // form may be saved at first weighing with only Tare filled in, so
        // GrossWeight has no Required / Range validation. The Status field
        // ('InProgress' vs 'Completed') is what gates whether a weighment is
        // billable; downstream code already treats GrossWeight = 0 as
        // "not yet weighed" and skips financial calculations.
        [Display(Name = "Gross Weight (tons)")]
        public decimal GrossWeight { get; set; }

        [NotMapped]
        [Display(Name = "Net Weight (tons)")]
        public decimal NetWeight => GrossWeight - (TareWeight ?? 0);

        [Required]
        [StringLength(10)]
        [Display(Name = "Weight Unit")]
        public string WeightUnit { get; set; } = "Ton";

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

        /// <summary>
        /// Per-line rebate (customer's per-unit rebate × net tons), surfaced on
        /// the form so the operator can see how much the customer is being
        /// discounted. Filled by the controller from <see cref="WeighmentTransaction.RebateAmount"/>
        /// on Edit, recomputed on POST so it always matches current customer settings.
        /// </summary>
        [Display(Name = "Rebate Amount")]
        [DataType(DataType.Currency)]
        public decimal? RebateAmount { get; set; }

        [Display(Name = "Total Amount")]
        [DataType(DataType.Currency)]
        public decimal? TotalAmount { get; set; }

        // Helper method to calculate financials.
        // NetWeight = Gross − Tare is the material loaded and is what we bill.
        public void CalculateFinancials()
        {
            if (NetWeight > 0 && PricePerUnit.HasValue)
            {
                // NetWeight is already in the chosen WeightUnit. Only convert
                // when the unit is kg (legacy / mixed-unit scenarios).
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
        
        [Display(Name = "Total Weight (tons)")]
        [DisplayFormat(DataFormatString = "{0:N3}")]
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