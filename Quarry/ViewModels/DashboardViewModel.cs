using System.ComponentModel.DataAnnotations;

namespace QuarryManagementSystem.ViewModels
{
    public class DashboardViewModel
    {
        // Today's Statistics
        [Display(Name = "Today's Weighments")]
        public int TodayWeighments { get; set; }

        [Display(Name = "Today's Revenue")]
        [DataType(DataType.Currency)]
        public decimal TodayRevenue { get; set; }

        // Monthly Statistics
        [Display(Name = "Monthly Weighments")]
        public int MonthlyWeighments { get; set; }

        [Display(Name = "Monthly Revenue")]
        [DataType(DataType.Currency)]
        public decimal MonthlyRevenue { get; set; }

        // Customer Statistics
        [Display(Name = "Total Customers")]
        public int TotalCustomers { get; set; }

        [Display(Name = "Active Customers")]
        public int ActiveCustomers { get; set; }

        // Material Statistics
        [Display(Name = "Total Materials")]
        public int TotalMaterials { get; set; }

        // Invoice Statistics
        [Display(Name = "Total Invoices")]
        public int TotalInvoices { get; set; }

        [Display(Name = "Unpaid Invoices")]
        public int UnpaidInvoices { get; set; }

        [Display(Name = "Overdue Invoices")]
        public int OverdueInvoices { get; set; }

        [Display(Name = "Outstanding Amount")]
        [DataType(DataType.Currency)]
        public decimal OutstandingAmount { get; set; }

        // Recent Activity
        public List<RecentTransactionViewModel> RecentTransactions { get; set; } = new();
        public List<MaterialStockViewModel> LowStockMaterials { get; set; } = new();
        public List<MonthlyTrendData> MonthlyTrend { get; set; } = new();

        // User Information
        public UserViewModel? CurrentUser { get; set; }

        // Error Handling
        public string? ErrorMessage { get; set; }

        // Calculated Properties
        [Display(Name = "Average Transaction Value")]
        [DataType(DataType.Currency)]
        public decimal AverageTransactionValue => MonthlyWeighments > 0 ? MonthlyRevenue / MonthlyWeighments : 0;

        [Display(Name = "Customer Retention Rate")]
        [DisplayFormat(DataFormatString = "{0:P1}")]
        public double CustomerRetentionRate => TotalCustomers > 0 ? (double)ActiveCustomers / TotalCustomers * 100 : 0;

        [Display(Name = "Collection Rate")]
        [DisplayFormat(DataFormatString = "{0:P1}")]
        public double CollectionRate
        {
            get
            {
                var totalInvoiceAmount = TotalInvoices > 0 ? TotalInvoices * 100000m : 0; // Estimate
                return totalInvoiceAmount > 0 ? (double)((totalInvoiceAmount - OutstandingAmount) / totalInvoiceAmount) * 100 : 0;
            }
        }
    }

    public class RecentTransactionViewModel
    {
        public int Id { get; set; }

        [Display(Name = "Transaction Number")]
        public string TransactionNumber { get; set; } = string.Empty;

        [Display(Name = "Date")]
        [DataType(DataType.Date)]
        public DateTime TransactionDate { get; set; }

        [Display(Name = "Customer")]
        public string CustomerName { get; set; } = string.Empty;

        [Display(Name = "Material")]
        public string MaterialName { get; set; } = string.Empty;

        [Display(Name = "Vehicle")]
        public string VehicleRegNumber { get; set; } = string.Empty;

        [Display(Name = "Net Weight (kg)")]
        [DisplayFormat(DataFormatString = "{0:N0}")]
        public decimal NetWeight { get; set; }

        [Display(Name = "Amount")]
        [DataType(DataType.Currency)]
        public decimal TotalAmount { get; set; }

        [Display(Name = "Status")]
        public string Status { get; set; } = string.Empty;
    }

    public class MaterialStockViewModel
    {
        public int Id { get; set; }

        [Display(Name = "Material Name")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Type")]
        public string Type { get; set; } = string.Empty;

        [Display(Name = "Unit Price")]
        [DataType(DataType.Currency)]
        public decimal UnitPrice { get; set; }

        [Display(Name = "Current Stock")]
        [DisplayFormat(DataFormatString = "{0:N2}")]
        public decimal CurrentStock { get; set; }

        [Display(Name = "Minimum Stock")]
        [DisplayFormat(DataFormatString = "{0:N2}")]
        public decimal MinimumStock { get; set; }

        [Display(Name = "Stock Status")]
        public string StockStatus => CurrentStock <= 0 ? "Out of Stock" : 
                                   CurrentStock < MinimumStock ? "Low Stock" : "In Stock";

        [Display(Name = "Reorder Required")]
        public bool ReorderRequired => CurrentStock < MinimumStock;
    }

    public class MonthlyTrendData
    {
        [Display(Name = "Month")]
        public string Month { get; set; } = string.Empty;

        [Display(Name = "Weighment Revenue")]
        [DataType(DataType.Currency)]
        public decimal WeighmentRevenue { get; set; }

        [Display(Name = "Invoice Amount")]
        [DataType(DataType.Currency)]
        public decimal InvoiceAmount { get; set; }

        [Display(Name = "Transaction Count")]
        public int TransactionCount { get; set; }

        [Display(Name = "Average Transaction Value")]
        [DataType(DataType.Currency)]
        public decimal AverageTransactionValue => TransactionCount > 0 ? WeighmentRevenue / TransactionCount : 0;
    }

    public class UserViewModel
    {
        public string Id { get; set; } = string.Empty;

        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Role")]
        public string Role { get; set; } = string.Empty;

        [Display(Name = "Last Login")]
        [DataType(DataType.DateTime)]
        public DateTime? LastLogin { get; set; }

        [Display(Name = "Last Login Display")]
        public string LastLoginDisplay => LastLogin.HasValue ? LastLogin.Value.ToString("dd/MM/yyyy HH:mm") : "Never";
    }

    public class ErrorViewModel
    {
        [Display(Name = "Request ID")]
        public string? RequestId { get; set; }

        [Display(Name = "Show Request ID")]
        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    }
}