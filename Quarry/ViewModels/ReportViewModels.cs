using System.ComponentModel.DataAnnotations;

namespace QuarryManagementSystem.ViewModels
{
    public class ReportDashboardViewModel
    {
        // Financial Summary
        [Display(Name = "Total Revenue")]
        [DataType(DataType.Currency)]
        public decimal TotalRevenue { get; set; }

        [Display(Name = "Monthly Revenue")]
        [DataType(DataType.Currency)]
        public decimal MonthlyRevenue { get; set; }

        [Display(Name = "Outstanding Amount")]
        [DataType(DataType.Currency)]
        public decimal OutstandingAmount { get; set; }

        [Display(Name = "Overdue Amount")]
        [DataType(DataType.Currency)]
        public decimal OverdueAmount { get; set; }

        // Operational Summary
        [Display(Name = "Total Weighments")]
        public int TotalWeighments { get; set; }

        [Display(Name = "Monthly Weighments")]
        public int MonthlyWeighments { get; set; }

        [Display(Name = "Active Customers")]
        public int ActiveCustomers { get; set; }

        [Display(Name = "Total Materials")]
        public int TotalMaterials { get; set; }

        // Employee Summary
        [Display(Name = "Total Employees")]
        public int TotalEmployees { get; set; }

        [Display(Name = "Active Employees")]
        public int ActiveEmployees { get; set; }

        [Display(Name = "Monthly Payroll")]
        [DataType(DataType.Currency)]
        public decimal MonthlyPayroll { get; set; }

        // Charts Data
        public List<MonthlyRevenueData> MonthlyRevenueChart { get; set; } = new();
        public List<DailyWeighmentData> DailyWeighmentsChart { get; set; } = new();
        public List<MaterialSalesChartData> MaterialSalesChart { get; set; } = new();
        public List<CustomerOutstandingData> CustomerOutstandingChart { get; set; } = new();

        // Alerts
        public List<LowStockMaterialData> LowStockMaterials { get; set; } = new();
        public List<OverdueInvoiceData> OverdueInvoices { get; set; } = new();
        public List<PendingPayrollData> PendingPayroll { get; set; } = new();

        // Quick Stats
        [Display(Name = "Average Transaction Value")]
        [DataType(DataType.Currency)]
        public decimal AverageTransactionValue { get; set; }

        [Display(Name = "Collection Rate")]
        [DisplayFormat(DataFormatString = "{0:P1}")]
        public double CollectionRate { get; set; }

        [Display(Name = "Employee Retention Rate")]
        [DisplayFormat(DataFormatString = "{0:P1}")]
        public double EmployeeRetentionRate { get; set; }

        public string? ErrorMessage { get; set; }
    }

    public class MonthlyRevenueData
    {
        [Display(Name = "Month")]
        public string Month { get; set; } = string.Empty;

        [Display(Name = "Revenue")]
        [DataType(DataType.Currency)]
        public decimal Revenue { get; set; }
    }

    public class DailyWeighmentData
    {
        [Display(Name = "Date")]
        public string Date { get; set; } = string.Empty;

        [Display(Name = "Count")]
        public int Count { get; set; }

        [Display(Name = "Total Weight (kg)")]
        [DisplayFormat(DataFormatString = "{0:N0}")]
        public decimal TotalWeight { get; set; }
    }

    public class MaterialSalesChartData
    {
        [Display(Name = "Material")]
        public string MaterialName { get; set; } = string.Empty;

        [Display(Name = "Total Quantity (kg)")]
        [DisplayFormat(DataFormatString = "{0:N0}")]
        public decimal TotalQuantity { get; set; }

        [Display(Name = "Total Revenue")]
        [DataType(DataType.Currency)]
        public decimal TotalRevenue { get; set; }
    }

    public class CustomerOutstandingData
    {
        [Display(Name = "Customer")]
        public string CustomerName { get; set; } = string.Empty;

        [Display(Name = "Outstanding Balance")]
        [DataType(DataType.Currency)]
        public decimal OutstandingBalance { get; set; }

        [Display(Name = "Credit Limit")]
        [DataType(DataType.Currency)]
        public decimal CreditLimit { get; set; }

        [Display(Name = "Utilization %")]
        [DisplayFormat(DataFormatString = "{0:P1}")]
        public double UtilizationPercentage => CreditLimit > 0 ? (double)(OutstandingBalance / CreditLimit) * 100 : 0;
    }

    public class LowStockMaterialData
    {
        [Display(Name = "Material")]
        public string MaterialName { get; set; } = string.Empty;

        [Display(Name = "Current Stock")]
        [DisplayFormat(DataFormatString = "{0:N2}")]
        public decimal CurrentStock { get; set; }

        [Display(Name = "Available Stock")]
        [DisplayFormat(DataFormatString = "{0:N2}")]
        public decimal AvailableStock { get; set; }

        [Display(Name = "Minimum Stock")]
        [DisplayFormat(DataFormatString = "{0:N2}")]
        public decimal MinimumStock { get; set; }

        [Display(Name = "Status")]
        public string Status => AvailableStock <= 0 ? "Out of Stock" : AvailableStock < MinimumStock ? "Low Stock" : "In Stock";
    }

    public class OverdueInvoiceData
    {
        [Display(Name = "Invoice #")]
        public string InvoiceNumber { get; set; } = string.Empty;

        [Display(Name = "Customer")]
        public string CustomerName { get; set; } = string.Empty;

        [Display(Name = "Outstanding Amount")]
        [DataType(DataType.Currency)]
        public decimal OutstandingAmount { get; set; }

        [Display(Name = "Days Overdue")]
        public int DaysOverdue { get; set; }
    }

    public class PendingPayrollData
    {
        [Display(Name = "Run Number")]
        public string RunNumber { get; set; } = string.Empty;

        [Display(Name = "Payment Month")]
        public string PaymentMonth { get; set; } = string.Empty;

        [Display(Name = "Total Employees")]
        public int TotalEmployees { get; set; }

        [Display(Name = "Net Pay")]
        [DataType(DataType.Currency)]
        public decimal NetPay { get; set; }

        [Display(Name = "Status")]
        public string Status { get; set; } = string.Empty;
    }

    public class FinancialReportViewModel
    {
        [Display(Name = "Report Type")]
        public string ReportType { get; set; } = "summary";

        [Display(Name = "Report Title")]
        public string ReportTitle { get; set; } = "Financial Report";

        [Display(Name = "From Date")]
        [DataType(DataType.Date)]
        public DateTime DateFrom { get; set; }

        [Display(Name = "To Date")]
        [DataType(DataType.Date)]
        public DateTime DateTo { get; set; }

        public ReportCompanyDetailsViewModel CompanyDetails { get; set; } = new();

        // Report Data
        public FinancialSummaryReport? FinancialSummary { get; set; }
        public TrialBalanceReport? TrialBalance { get; set; }
        public ProfitLossReport? ProfitLoss { get; set; }
        public BalanceSheetReport? BalanceSheet { get; set; }
        public CashFlowReport? CashFlow { get; set; }

        public string? ErrorMessage { get; set; }
    }

    public class FinancialSummaryReport
    {
        [Display(Name = "From Date")]
        [DataType(DataType.Date)]
        public DateTime FromDate { get; set; }

        [Display(Name = "To Date")]
        [DataType(DataType.Date)]
        public DateTime ToDate { get; set; }

        [Display(Name = "Total Revenue")]
        [DataType(DataType.Currency)]
        public decimal TotalRevenue { get; set; }

        [Display(Name = "Total Expenses")]
        [DataType(DataType.Currency)]
        public decimal TotalExpenses { get; set; }

        [Display(Name = "Net Profit")]
        [DataType(DataType.Currency)]
        public decimal NetProfit { get; set; }

        [Display(Name = "Profit Margin %")]
        [DisplayFormat(DataFormatString = "{0:P2}")]
        public double ProfitMargin => TotalRevenue > 0 ? (double)(NetProfit / TotalRevenue) * 100 : 0;

        [Display(Name = "Total Assets")]
        [DataType(DataType.Currency)]
        public decimal TotalAssets { get; set; }

        [Display(Name = "Total Liabilities")]
        [DataType(DataType.Currency)]
        public decimal TotalLiabilities { get; set; }

        [Display(Name = "Total Equity")]
        [DataType(DataType.Currency)]
        public decimal TotalEquity { get; set; }

        [Display(Name = "Current Ratio")]
        [DisplayFormat(DataFormatString = "{0:F2}")]
        public double CurrentRatio => TotalLiabilities > 0 ? (double)(TotalAssets / TotalLiabilities) : 0;
    }

    public class TrialBalanceReport
    {
        [Display(Name = "From Date")]
        [DataType(DataType.Date)]
        public DateTime FromDate { get; set; }

        [Display(Name = "To Date")]
        [DataType(DataType.Date)]
        public DateTime ToDate { get; set; }

        public List<TrialBalanceAccount> Accounts { get; set; } = new();

        [Display(Name = "Total Debit")]
        [DataType(DataType.Currency)]
        public decimal TotalDebit { get; set; }

        [Display(Name = "Total Credit")]
        [DataType(DataType.Currency)]
        public decimal TotalCredit { get; set; }

        [Display(Name = "Is Balanced")]
        public bool IsBalanced => TotalDebit == TotalCredit;
    }

    public class TrialBalanceAccount
    {
        [Display(Name = "Account Code")]
        public string AccountCode { get; set; } = string.Empty;

        [Display(Name = "Account Name")]
        public string AccountName { get; set; } = string.Empty;

        [Display(Name = "Account Type")]
        public string AccountType { get; set; } = string.Empty;

        [Display(Name = "Debit Balance")]
        [DataType(DataType.Currency)]
        public decimal DebitBalance { get; set; }

        [Display(Name = "Credit Balance")]
        [DataType(DataType.Currency)]
        public decimal CreditBalance { get; set; }
    }

    public class ProfitLossReport
    {
        [Display(Name = "From Date")]
        [DataType(DataType.Date)]
        public DateTime FromDate { get; set; }

        [Display(Name = "To Date")]
        [DataType(DataType.Date)]
        public DateTime ToDate { get; set; }

        [Display(Name = "Total Revenue")]
        [DataType(DataType.Currency)]
        public decimal Revenue { get; set; }

        [Display(Name = "Total Expenses")]
        [DataType(DataType.Currency)]
        public decimal Expenses { get; set; }

        [Display(Name = "Net Profit")]
        [DataType(DataType.Currency)]
        public decimal NetProfit { get; set; }

        [Display(Name = "Profit Margin %")]
        [DisplayFormat(DataFormatString = "{0:P2}")]
        public double ProfitMargin => Revenue > 0 ? (double)(NetProfit / Revenue) * 100 : 0;
    }

    public class BalanceSheetReport
    {
        [Display(Name = "As Of Date")]
        [DataType(DataType.Date)]
        public DateTime AsOfDate { get; set; }

        [Display(Name = "Total Assets")]
        [DataType(DataType.Currency)]
        public decimal TotalAssets { get; set; }

        [Display(Name = "Total Liabilities")]
        [DataType(DataType.Currency)]
        public decimal TotalLiabilities { get; set; }

        [Display(Name = "Total Equity")]
        [DataType(DataType.Currency)]
        public decimal TotalEquity { get; set; }

        [Display(Name = "Accounting Equation Check")]
        public bool IsBalanced => TotalAssets == (TotalLiabilities + TotalEquity);
    }

    public class CashFlowReport
    {
        [Display(Name = "From Date")]
        [DataType(DataType.Date)]
        public DateTime FromDate { get; set; }

        [Display(Name = "To Date")]
        [DataType(DataType.Date)]
        public DateTime ToDate { get; set; }

        [Display(Name = "Operating Activities")]
        [DataType(DataType.Currency)]
        public decimal OperatingActivities { get; set; }

        [Display(Name = "Investing Activities")]
        [DataType(DataType.Currency)]
        public decimal InvestingActivities { get; set; }

        [Display(Name = "Financing Activities")]
        [DataType(DataType.Currency)]
        public decimal FinancingActivities { get; set; }

        [Display(Name = "Net Cash Flow")]
        [DataType(DataType.Currency)]
        public decimal NetCashFlow => OperatingActivities + InvestingActivities + FinancingActivities;
    }

    public class OperationalReportViewModel
    {
        [Display(Name = "Report Type")]
        public string ReportType { get; set; } = "summary";

        [Display(Name = "Report Title")]
        public string ReportTitle { get; set; } = "Operational Report";

        [Display(Name = "From Date")]
        [DataType(DataType.Date)]
        public DateTime DateFrom { get; set; }

        [Display(Name = "To Date")]
        [DataType(DataType.Date)]
        public DateTime DateTo { get; set; }

        // Report Data
        public OperationalSummaryReport? OperationalSummary { get; set; }
        public DailyOperationsReport? DailyOperations { get; set; }
        public CustomerReport? CustomerReport { get; set; }
        public MaterialReport? MaterialReport { get; set; }
        public VehicleReport? VehicleReport { get; set; }

        public string? ErrorMessage { get; set; }
    }

    public class OperationalSummaryReport
    {
        [Display(Name = "From Date")]
        [DataType(DataType.Date)]
        public DateTime FromDate { get; set; }

        [Display(Name = "To Date")]
        [DataType(DataType.Date)]
        public DateTime ToDate { get; set; }

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
        public decimal AverageTransactionValue { get; set; }

        public List<TopMaterialData> TopMaterials { get; set; } = new();
        public List<TopCustomerData> TopCustomers { get; set; } = new();
    }

    public class DailyOperationsReport
    {
        [Display(Name = "From Date")]
        [DataType(DataType.Date)]
        public DateTime FromDate { get; set; }

        [Display(Name = "To Date")]
        [DataType(DataType.Date)]
        public DateTime ToDate { get; set; }

        public List<DailyOperationData> DailyOperations { get; set; } = new();
    }

    public class DailyOperationData
    {
        [Display(Name = "Date")]
        [DataType(DataType.Date)]
        public DateTime Date { get; set; }

        [Display(Name = "Transactions")]
        public int TransactionCount { get; set; }

        [Display(Name = "Total Weight (kg)")]
        [DisplayFormat(DataFormatString = "{0:N0}")]
        public decimal TotalWeight { get; set; }

        [Display(Name = "Revenue")]
        [DataType(DataType.Currency)]
        public decimal Revenue { get; set; }

        [Display(Name = "Top Material")]
        public string TopMaterial { get; set; } = string.Empty;

        [Display(Name = "Top Customer")]
        public string TopCustomer { get; set; } = string.Empty;
    }

    public class CustomerReport
    {
        [Display(Name = "From Date")]
        [DataType(DataType.Date)]
        public DateTime FromDate { get; set; }

        [Display(Name = "To Date")]
        [DataType(DataType.Date)]
        public DateTime ToDate { get; set; }

        public List<CustomerAnalysisData> CustomerAnalysis { get; set; } = new();
        public List<CustomerTransactionData> TopCustomers { get; set; } = new();
        public List<CustomerCreditData> CreditAnalysis { get; set; } = new();
    }

    public class CustomerAnalysisData
    {
        [Display(Name = "Customer")]
        public string CustomerName { get; set; } = string.Empty;

        [Display(Name = "Transactions")]
        public int TransactionCount { get; set; }

        [Display(Name = "Total Weight (kg)")]
        [DisplayFormat(DataFormatString = "{0:N0}")]
        public decimal TotalWeight { get; set; }

        [Display(Name = "Total Revenue")]
        [DataType(DataType.Currency)]
        public decimal TotalRevenue { get; set; }

        [Display(Name = "Average Transaction Value")]
        [DataType(DataType.Currency)]
        public decimal AverageTransactionValue { get; set; }

        [Display(Name = "Last Transaction")]
        [DataType(DataType.Date)]
        public DateTime? LastTransactionDate { get; set; }
    }

    public class MaterialReport
    {
        [Display(Name = "From Date")]
        [DataType(DataType.Date)]
        public DateTime FromDate { get; set; }

        [Display(Name = "To Date")]
        [DataType(DataType.Date)]
        public DateTime ToDate { get; set; }

        public List<MaterialSalesReportData> MaterialSales { get; set; } = new();
        public List<MaterialStockReportData> MaterialStock { get; set; } = new();
        public List<MaterialPriceReportData> PriceAnalysis { get; set; } = new();
    }

    public class MaterialSalesReportData
    {
        [Display(Name = "Material")]
        public string MaterialName { get; set; } = string.Empty;

        [Display(Name = "Type")]
        public string MaterialType { get; set; } = string.Empty;

        [Display(Name = "Quantity Sold (kg)")]
        [DisplayFormat(DataFormatString = "{0:N0}")]
        public decimal QuantitySold { get; set; }

        [Display(Name = "Revenue")]
        [DataType(DataType.Currency)]
        public decimal Revenue { get; set; }

        [Display(Name = "Average Price")]
        [DataType(DataType.Currency)]
        public decimal AveragePrice { get; set; }

        [Display(Name = "Market Share %")]
        [DisplayFormat(DataFormatString = "{0:P1}")]
        public double MarketShare { get; set; }
    }

    public class TaxReportViewModel
    {
        [Display(Name = "Report Type")]
        public string ReportType { get; set; } = "vat";

        [Display(Name = "Report Title")]
        public string ReportTitle { get; set; } = "Tax Report";

        [Display(Name = "Year")]
        public int Year { get; set; }

        [Display(Name = "Month")]
        public int Month { get; set; }

        public ReportCompanyDetailsViewModel CompanyDetails { get; set; } = new();

        // Report Data
        public TaxSummaryReport? TaxSummary { get; set; }
        public VATReport? VATReport { get; set; }
        public PAYEReport? PAYEReport { get; set; }
        public PensionReport? PensionReport { get; set; }
        public ComplianceReport? ComplianceReport { get; set; }

        public string? ErrorMessage { get; set; }
    }

    public class MaterialStockReportData
    {
        [Display(Name = "Material")]
        public string MaterialName { get; set; } = string.Empty;

        [Display(Name = "Type")]
        public string MaterialType { get; set; } = string.Empty;

        [Display(Name = "Current Stock")]
        [DisplayFormat(DataFormatString = "{0:N2}")]
        public decimal CurrentStock { get; set; }

        [Display(Name = "Reserved Stock")]
        [DisplayFormat(DataFormatString = "{0:N2}")]
        public decimal ReservedStock { get; set; }

        [Display(Name = "Available Stock")]
        [DisplayFormat(DataFormatString = "{0:N2}")]
        public decimal AvailableStock { get; set; }

        [Display(Name = "Unit Price")]
        [DataType(DataType.Currency)]
        public decimal UnitPrice { get; set; }

        [Display(Name = "Stock Value")]
        [DataType(DataType.Currency)]
        public decimal StockValue { get; set; }

        [Display(Name = "Status")]
        public string Status { get; set; } = string.Empty;
    }

    public class MaterialPriceReportData
    {
        [Display(Name = "Material")]
        public string MaterialName { get; set; } = string.Empty;

        [Display(Name = "Type")]
        public string MaterialType { get; set; } = string.Empty;

        [Display(Name = "Current Price")]
        [DataType(DataType.Currency)]
        public decimal CurrentPrice { get; set; }

        [Display(Name = "Previous Price")]
        [DataType(DataType.Currency)]
        public decimal PreviousPrice { get; set; }

        [Display(Name = "Price Change")]
        [DataType(DataType.Currency)]
        public decimal PriceChange => CurrentPrice - PreviousPrice;

        [Display(Name = "Percentage Change")]
        [DisplayFormat(DataFormatString = "{0:P2}")]
        public double PercentageChange => PreviousPrice > 0 ? (double)((CurrentPrice - PreviousPrice) / PreviousPrice) * 100 : 0;
    }

    public class StockMovementReport
    {
        [Display(Name = "Material")]
        public string MaterialName { get; set; } = string.Empty;

        [Display(Name = "Opening Stock")]
        [DisplayFormat(DataFormatString = "{0:N2}")]
        public decimal OpeningStock { get; set; }

        [Display(Name = "Stock In")]
        [DisplayFormat(DataFormatString = "{0:N2}")]
        public decimal StockIn { get; set; }

        [Display(Name = "Stock Out")]
        [DisplayFormat(DataFormatString = "{0:N2}")]
        public decimal StockOut { get; set; }

        [Display(Name = "Closing Stock")]
        [DisplayFormat(DataFormatString = "{0:N2}")]
        public decimal ClosingStock { get; set; }

        [Display(Name = "Movement Date")]
        [DataType(DataType.Date)]
        public DateTime MovementDate { get; set; }
    }

    public class StockValuationReport
    {
        [Display(Name = "Material")]
        public string MaterialName { get; set; } = string.Empty;

        [Display(Name = "Current Stock")]
        [DisplayFormat(DataFormatString = "{0:N2}")]
        public decimal CurrentStock { get; set; }

        [Display(Name = "Unit Price")]
        [DataType(DataType.Currency)]
        public decimal UnitPrice { get; set; }

        [Display(Name = "Total Value")]
        [DataType(DataType.Currency)]
        public decimal TotalValue { get; set; }

        [Display(Name = "Valuation Date")]
        [DataType(DataType.Date)]
        public DateTime ValuationDate { get; set; }
    }

    public class ReorderReport
    {
        [Display(Name = "Material")]
        public string MaterialName { get; set; } = string.Empty;

        [Display(Name = "Type")]
        public string MaterialType { get; set; } = string.Empty;

        [Display(Name = "Current Stock")]
        [DisplayFormat(DataFormatString = "{0:N2}")]
        public decimal CurrentStock { get; set; }

        [Display(Name = "Minimum Stock")]
        [DisplayFormat(DataFormatString = "{0:N2}")]
        public decimal MinimumStock { get; set; }

        [Display(Name = "Reorder Quantity")]
        [DisplayFormat(DataFormatString = "{0:N2}")]
        public decimal ReorderQuantity { get; set; }

        [Display(Name = "Priority")]
        public string Priority { get; set; } = string.Empty;

        [Display(Name = "Last Order Date")]
        [DataType(DataType.Date)]
        public DateTime? LastOrderDate { get; set; }
    }

    public class VehicleReport
    {
        [Display(Name = "Vehicle Registration")]
        public string VehicleRegNumber { get; set; } = string.Empty;

        [Display(Name = "Customer")]
        public string CustomerName { get; set; } = string.Empty;

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
        public decimal AverageTransactionValue { get; set; }

        [Display(Name = "Last Transaction Date")]
        [DataType(DataType.Date)]
        public DateTime? LastTransactionDate { get; set; }
    }

    public class CustomerTransactionData
    {
        [Display(Name = "Customer")]
        public string CustomerName { get; set; } = string.Empty;

        [Display(Name = "Transactions")]
        public int TransactionCount { get; set; }

        [Display(Name = "Total Weight (kg)")]
        [DisplayFormat(DataFormatString = "{0:N0}")]
        public decimal TotalWeight { get; set; }

        [Display(Name = "Total Revenue")]
        [DataType(DataType.Currency)]
        public decimal TotalRevenue { get; set; }

        [Display(Name = "Average Transaction Value")]
        [DataType(DataType.Currency)]
        public decimal AverageTransactionValue { get; set; }

        [Display(Name = "Market Share %")]
        [DisplayFormat(DataFormatString = "{0:P1}")]
        public double MarketShare { get; set; }
    }

    public class CustomerCreditData
    {
        [Display(Name = "Customer")]
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

        [Display(Name = "Utilization %")]
        [DisplayFormat(DataFormatString = "{0:P1}")]
        public double UtilizationPercentage { get; set; }

        [Display(Name = "Risk Level")]
        public string RiskLevel { get; set; } = string.Empty;
    }

    public class TaxSummaryReport
    {
        [Display(Name = "Year")]
        public int Year { get; set; }

        [Display(Name = "Month")]
        public int Month { get; set; }

        [Display(Name = "Period")]
        public string Period { get; set; } = string.Empty;

        [Display(Name = "VAT Output")]
        [DataType(DataType.Currency)]
        public decimal VATOutput { get; set; }

        [Display(Name = "VAT Input")]
        [DataType(DataType.Currency)]
        public decimal VATInput { get; set; }

        [Display(Name = "Net VAT")]
        [DataType(DataType.Currency)]
        public decimal NetVAT { get; set; }

        [Display(Name = "PAYE Due")]
        [DataType(DataType.Currency)]
        public decimal PAYEReported { get; set; }

        [Display(Name = "Pension Due")]
        [DataType(DataType.Currency)]
        public decimal PensionReported { get; set; }

        [Display(Name = "NHIS Due")]
        [DataType(DataType.Currency)]
        public decimal NHISReported { get; set; }

        [Display(Name = "Total Tax Due")]
        [DataType(DataType.Currency)]
        public decimal TotalTaxDue => NetVAT + PAYEReported + PensionReported + NHISReported;
    }

    public class VATReport
    {
        [Display(Name = "Year")]
        public int Year { get; set; }

        [Display(Name = "Month")]
        public int Month { get; set; }

        [Display(Name = "Period")]
        public string Period { get; set; } = string.Empty;

        [Display(Name = "Output VAT (Sales)")]
        [DataType(DataType.Currency)]
        public decimal OutputVAT { get; set; }

        [Display(Name = "Input VAT (Purchases)")]
        [DataType(DataType.Currency)]
        public decimal InputVAT { get; set; }

        [Display(Name = "Net VAT")]
        [DataType(DataType.Currency)]
        public decimal NetVAT { get; set; }

        [Display(Name = "VAT Payable")]
        [DataType(DataType.Currency)]
        public decimal VATPayable { get; set; }

        [Display(Name = "VAT Receivable")]
        [DataType(DataType.Currency)]
        public decimal VATReceivable { get; set; }

        [Display(Name = "Remittance Due Date")]
        [DataType(DataType.Date)]
        public DateTime RemittanceDueDate { get; set; }
    }

    public class PAYEReport
    {
        [Display(Name = "Year")]
        public int Year { get; set; }

        [Display(Name = "Month")]
        public int Month { get; set; }

        [Display(Name = "Period")]
        public string Period { get; set; } = string.Empty;

        [Display(Name = "Total PAYE")]
        [DataType(DataType.Currency)]
        public decimal TotalPAYE { get; set; }

        [Display(Name = "Employee Count")]
        public int EmployeeCount { get; set; }

        [Display(Name = "Monthly Remittance")]
        [DataType(DataType.Currency)]
        public decimal MonthlyRemittance { get; set; }

        [Display(Name = "Remittance Due Date")]
        [DataType(DataType.Date)]
        public DateTime DueDate { get; set; }

        [Display(Name = "Days Until Due")]
        public int DaysUntilDue => (DueDate - DateTime.Now).Days;
    }

    public class PensionReport
    {
        [Display(Name = "Year")]
        public int Year { get; set; }

        [Display(Name = "Month")]
        public int Month { get; set; }

        [Display(Name = "Period")]
        public string Period { get; set; } = string.Empty;

        [Display(Name = "Employee Contributions")]
        [DataType(DataType.Currency)]
        public decimal EmployeeContributions { get; set; }

        [Display(Name = "Employer Contributions")]
        [DataType(DataType.Currency)]
        public decimal EmployerContributions { get; set; }

        [Display(Name = "Total Contributions")]
        [DataType(DataType.Currency)]
        public decimal TotalContributions { get; set; }

        [Display(Name = "Employee Count")]
        public int EmployeeCount { get; set; }

        [Display(Name = "Remittance Due Date")]
        [DataType(DataType.Date)]
        public DateTime DueDate { get; set; }
    }

    public class ComplianceReport
    {
        [Display(Name = "PAYE Remittance")]
        [DataType(DataType.Currency)]
        public decimal PAYERemittance { get; set; }

        [Display(Name = "Pension Remittance")]
        [DataType(DataType.Currency)]
        public decimal PensionRemittance { get; set; }

        [Display(Name = "NHIS Remittance")]
        [DataType(DataType.Currency)]
        public decimal NHISRemittance { get; set; }

        [Display(Name = "Total Remittance")]
        [DataType(DataType.Currency)]
        public decimal TotalRemittance => PAYERemittance + PensionRemittance + NHISRemittance;

        [Display(Name = "Employee Count")]
        public int EmployeeCount { get; set; }

        [Display(Name = "Compliance Status")]
        public string ComplianceStatus { get; set; } = string.Empty;

        [Display(Name = "Remittance Deadline")]
        [DataType(DataType.Date)]
        public DateTime RemittanceDeadline { get; set; }

        [Display(Name = "Days Until Deadline")]
        public int DaysUntilDeadline => (RemittanceDeadline - DateTime.Now).Days;
    }

    public class StockReportViewModel
    {
        [Display(Name = "Report Type")]
        public string ReportType { get; set; } = "summary";

        [Display(Name = "Report Title")]
        public string ReportTitle { get; set; } = "Stock Report";

        // Report Data
        public StockSummaryReport? StockSummary { get; set; }
        public StockMovementReport? StockMovement { get; set; }
        public StockValuationReport? StockValuation { get; set; }
        public ReorderReport? ReorderReport { get; set; }

        public string? ErrorMessage { get; set; }
    }

    public class StockSummaryReport
    {
        [Display(Name = "Total Materials")]
        public int TotalMaterials { get; set; }

        [Display(Name = "Total Stock Value")]
        [DataType(DataType.Currency)]
        public decimal TotalStockValue { get; set; }

        [Display(Name = "Low Stock Count")]
        public int LowStockCount { get; set; }

        [Display(Name = "Out of Stock Count")]
        public int OutOfStockCount { get; set; }

        public List<StockItem> StockItems { get; set; } = new();
    }

    public class StockItem
    {
        [Display(Name = "Material")]
        public string MaterialName { get; set; } = string.Empty;

        [Display(Name = "Type")]
        public string MaterialType { get; set; } = string.Empty;

        [Display(Name = "Current Stock")]
        [DisplayFormat(DataFormatString = "{0:N2}")]
        public decimal CurrentStock { get; set; }

        [Display(Name = "Reserved Stock")]
        [DisplayFormat(DataFormatString = "{0:N2}")]
        public decimal ReservedStock { get; set; }

        [Display(Name = "Available Stock")]
        [DisplayFormat(DataFormatString = "{0:N2}")]
        public decimal AvailableStock { get; set; }

        [Display(Name = "Unit Price")]
        [DataType(DataType.Currency)]
        public decimal UnitPrice { get; set; }

        [Display(Name = "Stock Value")]
        [DataType(DataType.Currency)]
        public decimal StockValue { get; set; }

        [Display(Name = "Status")]
        public string Status { get; set; } = string.Empty;
    }

    public class PayrollReportViewModel
    {
        [Display(Name = "Year")]
        public int Year { get; set; }

        [Display(Name = "Month")]
        public int Month { get; set; }

        [Display(Name = "Report Type")]
        public string ReportType { get; set; } = "summary";

        [Display(Name = "Report Title")]
        public string ReportTitle { get; set; } = "Payroll Report";

        // Report Data
        public PayrollSummaryReport? PayrollSummary { get; set; }
        public PayrollDetailsReport? PayrollDetails { get; set; }
        public ComplianceReport? ComplianceReport { get; set; }
        public BankPaymentReport? BankReport { get; set; }

        public string? ErrorMessage { get; set; }
    }

    public class PayrollSummaryReport
    {
        [Display(Name = "Year")]
        public int Year { get; set; }

        [Display(Name = "Month")]
        public int Month { get; set; }

        [Display(Name = "Period")]
        public string Period { get; set; } = string.Empty;

        [Display(Name = "Total Employees")]
        public int TotalEmployees { get; set; }

        [Display(Name = "Gross Pay")]
        [DataType(DataType.Currency)]
        public decimal GrossPay { get; set; }

        [Display(Name = "Net Pay")]
        [DataType(DataType.Currency)]
        public decimal NetPay { get; set; }

        [Display(Name = "Total PAYE")]
        [DataType(DataType.Currency)]
        public decimal TotalPAYE { get; set; }

        [Display(Name = "Total Pension")]
        [DataType(DataType.Currency)]
        public decimal TotalPension { get; set; }

        [Display(Name = "Total NHIS")]
        [DataType(DataType.Currency)]
        public decimal TotalNHIS { get; set; }

        [Display(Name = "Employer Contributions")]
        [DataType(DataType.Currency)]
        public decimal EmployerContributions { get; set; }
    }

    public class PayrollDetailsReport
    {
        [Display(Name = "Period")]
        public string Period { get; set; } = string.Empty;

        public List<EmployeeSalaryDetail> EmployeeSalaries { get; set; } = new();
    }

    public class EmployeeSalaryDetail
    {
        [Display(Name = "Employee Code")]
        public string EmployeeCode { get; set; } = string.Empty;

        [Display(Name = "Employee Name")]
        public string EmployeeName { get; set; } = string.Empty;

        [Display(Name = "Department")]
        public string Department { get; set; } = string.Empty;

        [Display(Name = "Basic Salary")]
        [DataType(DataType.Currency)]
        public decimal BasicSalary { get; set; }

        [Display(Name = "Gross Pay")]
        [DataType(DataType.Currency)]
        public decimal GrossPay { get; set; }

        [Display(Name = "PAYE")]
        [DataType(DataType.Currency)]
        public decimal PAYE { get; set; }

        [Display(Name = "Pension")]
        [DataType(DataType.Currency)]
        public decimal Pension { get; set; }

        [Display(Name = "NHIS")]
        [DataType(DataType.Currency)]
        public decimal NHIS { get; set; }

        [Display(Name = "Net Pay")]
        [DataType(DataType.Currency)]
        public decimal NetPay { get; set; }

        [Display(Name = "Bank Details")]
        public string BankDetails { get; set; } = string.Empty;
    }

    public class BankPaymentReport
    {
        [Display(Name = "Period")]
        public string Period { get; set; } = string.Empty;

        [Display(Name = "Total Amount")]
        [DataType(DataType.Currency)]
        public decimal TotalAmount { get; set; }

        [Display(Name = "Total Employees")]
        public int TotalEmployees { get; set; }

        public List<BankPaymentDetail> PaymentDetails { get; set; } = new();
    }

    public class BankPaymentDetail
    {
        [Display(Name = "Employee Code")]
        public string EmployeeCode { get; set; } = string.Empty;

        [Display(Name = "Employee Name")]
        public string EmployeeName { get; set; } = string.Empty;

        [Display(Name = "Bank Name")]
        public string BankName { get; set; } = string.Empty;

        [Display(Name = "Account Number")]
        public string AccountNumber { get; set; } = string.Empty;

        [Display(Name = "Net Pay")]
        [DataType(DataType.Currency)]
        public decimal NetPay { get; set; }

        [Display(Name = "Payment Reference")]
        public string PaymentReference { get; set; } = string.Empty;
    }

    // Helper classes for operational data
    public class TopMaterialData
    {
        [Display(Name = "Material")]
        public string MaterialName { get; set; } = string.Empty;

        [Display(Name = "Quantity Sold (kg)")]
        [DisplayFormat(DataFormatString = "{0:N0}")]
        public decimal QuantitySold { get; set; }

        [Display(Name = "Revenue")]
        [DataType(DataType.Currency)]
        public decimal Revenue { get; set; }

        [Display(Name = "Market Share %")]
        [DisplayFormat(DataFormatString = "{0:P1}")]
        public double MarketShare { get; set; }
    }

    public class TopCustomerData
    {
        [Display(Name = "Customer")]
        public string CustomerName { get; set; } = string.Empty;

        [Display(Name = "Transactions")]
        public int TransactionCount { get; set; }

        [Display(Name = "Total Weight (kg)")]
        [DisplayFormat(DataFormatString = "{0:N0}")]
        public decimal TotalWeight { get; set; }

        [Display(Name = "Revenue")]
        [DataType(DataType.Currency)]
        public decimal Revenue { get; set; }

        [Display(Name = "Market Share %")]
        [DisplayFormat(DataFormatString = "{0:P1}")]
        public double MarketShare { get; set; }
    }

    public class ReportCompanyDetailsViewModel
    {
        [Display(Name = "Company Name")]
        public string CompanyName { get; set; } = "Nigerian Quarry Management System";

        [Display(Name = "Address")]
        public string Address { get; set; } = "123 Quarry Road, Industrial Estate";

        [Display(Name = "City")]
        public string City { get; set; } = "Lagos";

        [Display(Name = "State")]
        public string State { get; set; } = "Lagos";

        [Display(Name = "Phone")]
        public string Phone { get; set; } = "+234-1-2345678";

        [Display(Name = "Email")]
        public string Email { get; set; } = "info@quarry.ng";

        [Display(Name = "Website")]
        public string Website { get; set; } = "www.quarry.ng";

        [Display(Name = "Tax Number")]
        public string TaxNumber { get; set; } = "12345678-0001";

        [Display(Name = "Bank Details")]
        public string BankDetails { get; set; } = "Access Bank, Account: 1234567890";
    }
}