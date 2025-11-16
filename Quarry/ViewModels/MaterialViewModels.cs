using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;
using QuarryManagementSystem.Models.Domain;

namespace QuarryManagementSystem.ViewModels
{
    public class MaterialListViewModel
    {
        public List<Material> Materials { get; set; } = new();
        
        [Display(Name = "Search Term")]
        public string? SearchTerm { get; set; }
        
        [Display(Name = "Type")]
        public string? SelectedType { get; set; }
        
        [Display(Name = "Status")]
        public string? SelectedStatus { get; set; }
        
        // Pagination
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; }
        public int TotalCount { get; set; }
        
        // Dropdown data
        public List<SelectListItem> Types { get; set; } = new();
        public List<SelectListItem> Statuses { get; set; } = new();
        
        // Helper properties
        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;
        
        public string? ErrorMessage { get; set; }
    }

    public class MaterialCreateViewModel
    {
        [Required(ErrorMessage = "Material name is required")]
        [StringLength(100)]
        [Display(Name = "Material Name")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Material type is required")]
        [StringLength(50)]
        [Display(Name = "Material Type")]
        public string Type { get; set; } = string.Empty;

        [Required(ErrorMessage = "Unit of measurement is required")]
        [StringLength(20)]
        [Display(Name = "Unit")]
        public string Unit { get; set; } = "Ton";

        [Required(ErrorMessage = "Unit price is required")]
        [Display(Name = "Unit Price")]
        [Range(0.01, 999999.99, ErrorMessage = "Unit price must be between 0.01 and 999,999.99")]
        public decimal UnitPrice { get; set; }

        [Display(Name = "VAT Rate (%)")]
        [Range(0, 100, ErrorMessage = "VAT rate must be between 0 and 100")]
        public decimal VatRate { get; set; } = 7.5m;

        [StringLength(20)]
        public string Status { get; set; } = "Active";

        // Dropdown data
        public List<SelectListItem> Types { get; set; } = new();
        public List<SelectListItem> Units { get; set; } = new();
        public List<SelectListItem> Statuses { get; set; } = new();

        // Common material types and units
        public static readonly string[] CommonTypes =
        {
            "Aggregate",
            "Sand", 
            "Dust",
            "Laterite",
            "Stone",
            "Gravel"
        };

        public static readonly string[] CommonUnits =
        {
            "Ton",
            "Kg",
            "Cubic Meter",
            "Cubic Feet"
        };
    }

    public class MaterialEditViewModel : MaterialCreateViewModel
    {
        public int Id { get; set; }

        [Display(Name = "Created Date")]
        [DataType(DataType.DateTime)]
        public DateTime CreatedAt { get; set; }

        [Display(Name = "Updated Date")]
        [DataType(DataType.DateTime)]
        public DateTime? UpdatedAt { get; set; }
    }

    public class MaterialDetailsViewModel
    {
        public Material Material { get; set; } = new();
        
        public List<MaterialStockLevelViewModel> StockLevels { get; set; } = new();
        
        [Display(Name = "Total Stock")]
        public decimal TotalStock => StockLevels.Sum(s => s.CurrentStock);
        
        [Display(Name = "Total Reserved")]
        public decimal TotalReserved => StockLevels.Sum(s => s.ReservedStock);
        
        [Display(Name = "Total Available")]
        public decimal TotalAvailable => StockLevels.Sum(s => s.AvailableStock);
        
        [Display(Name = "Recent Transactions")]
        public List<MaterialTransactionViewModel> RecentTransactions { get; set; } = new();
        
        [Display(Name = "Price History")]
        public List<MaterialPriceHistoryViewModel> PriceHistory { get; set; } = new();
    }

    public class MaterialStockLevelViewModel
    {
        public int StockYardId { get; set; }
        
        [Display(Name = "Quarry")]
        public string QuarryName { get; set; } = string.Empty;
        
        [Display(Name = "Current Stock")]
        [DisplayFormat(DataFormatString = "{0:N2}")]
        public decimal CurrentStock { get; set; }
        
        [Display(Name = "Reserved Stock")]
        [DisplayFormat(DataFormatString = "{0:N2}")]
        public decimal ReservedStock { get; set; }
        
        [Display(Name = "Available Stock")]
        [DisplayFormat(DataFormatString = "{0:N2}")]
        public decimal AvailableStock => CurrentStock - ReservedStock;
        
        [Display(Name = "Last Updated")]
        [DataType(DataType.DateTime)]
        public DateTime LastUpdated { get; set; }
        
        [Display(Name = "Stock Status")]
        public string StockStatus => CurrentStock <= 0 ? "Out of Stock" :
                                   AvailableStock < 10 ? "Low Stock" : "In Stock";
        
        [Display(Name = "Reorder Required")]
        public bool ReorderRequired => AvailableStock < 10;
    }

    public class MaterialTransactionViewModel
    {
        public int Id { get; set; }
        
        [Display(Name = "Transaction Number")]
        public string TransactionNumber { get; set; } = string.Empty;
        
        [Display(Name = "Date")]
        [DataType(DataType.Date)]
        public DateTime TransactionDate { get; set; }
        
        [Display(Name = "Customer")]
        public string CustomerName { get; set; } = string.Empty;
        
        [Display(Name = "Vehicle")]
        public string VehicleRegNumber { get; set; } = string.Empty;
        
        [Display(Name = "Net Weight (kg)")]
        [DisplayFormat(DataFormatString = "{0:N0}")]
        public decimal NetWeight { get; set; }
        
        [Display(Name = "Price Per Unit")]
        [DataType(DataType.Currency)]
        public decimal PricePerUnit { get; set; }
        
        [Display(Name = "Total Amount")]
        [DataType(DataType.Currency)]
        public decimal TotalAmount { get; set; }
        
        [Display(Name = "Status")]
        public string Status { get; set; } = string.Empty;
    }

    public class MaterialPriceHistoryViewModel
    {
        public int Id { get; set; }
        
        [Display(Name = "Old Price")]
        [DataType(DataType.Currency)]
        public decimal OldPrice { get; set; }
        
        [Display(Name = "New Price")]
        [DataType(DataType.Currency)]
        public decimal NewPrice { get; set; }
        
        [Display(Name = "Change Date")]
        [DataType(DataType.Date)]
        public DateTime ChangeDate { get; set; }
        
        [Display(Name = "Changed By")]
        public string ChangedBy { get; set; } = string.Empty;
        
        [Display(Name = "Reason")]
        public string Reason { get; set; } = string.Empty;
        
        [Display(Name = "Price Change")]
        [DataType(DataType.Currency)]
        public decimal PriceChange => NewPrice - OldPrice;
        
        [Display(Name = "Percentage Change")]
        [DisplayFormat(DataFormatString = "{0:P2}")]
        public double PercentageChange => OldPrice > 0 ? (double)((NewPrice - OldPrice) / OldPrice) * 100 : 0;
    }

    public class MaterialStockAdjustmentViewModel
    {
        public int MaterialId { get; set; }
        
        [Display(Name = "Material Name")]
        public string MaterialName { get; set; } = string.Empty;
        
        [Display(Name = "Current Total Stock")]
        [DisplayFormat(DataFormatString = "{0:N2}")]
        public decimal CurrentStock { get; set; }
        
        public List<StockYard> StockYards { get; set; } = new();
        
        [Display(Name = "Stock Adjustments")]
        public Dictionary<int, decimal> Adjustments { get; set; } = new();
        
        [Display(Name = "Reason for Adjustment")]
        [Required(ErrorMessage = "Reason for adjustment is required")]
        [StringLength(500)]
        public string AdjustmentReason { get; set; } = string.Empty;
        
        [Display(Name = "New Total Stock")]
        [DisplayFormat(DataFormatString = "{0:N2}")]
        public decimal NewTotalStock => CurrentStock + (Adjustments?.Values.Sum() ?? 0);
    }

    public class MaterialSummaryViewModel
    {
        [Display(Name = "Total Materials")]
        public int TotalMaterials { get; set; }
        
        [Display(Name = "Active Materials")]
        public int ActiveMaterials { get; set; }
        
        [Display(Name = "Total Stock Value")]
        [DataType(DataType.Currency)]
        public decimal TotalStockValue { get; set; }
        
        [Display(Name = "Low Stock Count")]
        public int LowStockCount { get; set; }
        
        [Display(Name = "Out of Stock Count")]
        public int OutOfStockCount { get; set; }
        
        [Display(Name = "Average Price")]
        [DataType(DataType.Currency)]
        public decimal AveragePrice { get; set; }
        
        public List<MaterialTypeSummaryViewModel> TypeSummaries { get; set; } = new();
    }

    public class MaterialTypeSummaryViewModel
    {
        [Display(Name = "Material Type")]
        public string Type { get; set; } = string.Empty;
        
        [Display(Name = "Count")]
        public int Count { get; set; }
        
        [Display(Name = "Total Stock")]
        [DisplayFormat(DataFormatString = "{0:N2}")]
        public decimal TotalStock { get; set; }
        
        [Display(Name = "Average Price")]
        [DataType(DataType.Currency)]
        public decimal AveragePrice { get; set; }
        
        [Display(Name = "Total Value")]
        [DataType(DataType.Currency)]
        public decimal TotalValue { get; set; }
    }

    public class MaterialFilterViewModel
    {
        [Display(Name = "Material Type")]
        public string? Type { get; set; }
        
        [Display(Name = "Status")]
        public string? Status { get; set; }
        
        [Display(Name = "Price Range From")]
        [DataType(DataType.Currency)]
        public decimal? PriceRangeFrom { get; set; }
        
        [Display(Name = "Price Range To")]
        [DataType(DataType.Currency)]
        public decimal? PriceRangeTo { get; set; }
        
        [Display(Name = "Stock Status")]
        public string? StockStatus { get; set; }
        
        // Dropdown data
        public List<SelectListItem> Types { get; set; } = new();
        public List<SelectListItem> Statuses { get; set; } = new();
        public List<SelectListItem> StockStatuses { get; set; } = new();
    }
}