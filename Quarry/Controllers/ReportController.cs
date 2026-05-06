using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuarryManagementSystem.Data;
using QuarryManagementSystem.Models.Domain;
using QuarryManagementSystem.ViewModels;
using QuarryManagementSystem.Utilities;
using System.Globalization;

namespace QuarryManagementSystem.Controllers
{
    [Authorize(Roles = "Admin,Manager,Accountant")]
    public class ReportController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ReportController> _logger;

        public ReportController(ApplicationDbContext context, ILogger<ReportController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: Report
        public IActionResult Index()
        {
            return View();
        }

        // GET: Report/Dashboard
        public async Task<IActionResult> Dashboard()
        {
            try
            {
                var today = DateTime.Today;
                var thisMonth = new DateTime(today.Year, today.Month, 1);
                var lastMonth = thisMonth.AddMonths(-1);

                var dashboardData = new ReportDashboardViewModel
                {
                    // Financial Summary
                    TotalRevenue = await _context.Invoices
                        .Where(i => i.Status == "Paid")
                        .SumAsync(i => i.PaidAmount),

                    MonthlyRevenue = await _context.Invoices
                        .Where(i => i.InvoiceDate >= thisMonth && i.Status == "Paid")
                        .SumAsync(i => i.PaidAmount),

                    OutstandingAmount = await _context.Invoices
                        .Where(i => i.Status == "Unpaid" || i.Status == "Partial")
                        .SumAsync(i => i.TotalAmount - i.PaidAmount),

                    OverdueAmount = await _context.Invoices
                        .Where(i => i.Status == "Overdue")
                        .SumAsync(i => i.TotalAmount - i.PaidAmount),

                    // Operational Summary
                    TotalWeighments = await _context.WeighmentTransactions.CountAsync(),
                    MonthlyWeighments = await _context.WeighmentTransactions
                        .CountAsync(w => w.TransactionDate >= thisMonth),
                    ActiveCustomers = await _context.Customers
                        .CountAsync(c => c.Status == "Active"),
                    TotalMaterials = await _context.Materials
                        .CountAsync(m => m.Status == "Active"),

                    // Employee Summary
                    TotalEmployees = await _context.Employees.CountAsync(),
                    ActiveEmployees = await _context.Employees
                        .CountAsync(e => e.Status == "Active"),
                    MonthlyPayroll = await _context.PayrollRuns
                        .Where(pr => pr.PaymentMonth >= thisMonth && pr.Status == "Processed")
                        .SumAsync(pr => pr.NetPay),

                    // Charts Data
                    MonthlyRevenueChart = await GetMonthlyRevenueChartData(),
                    DailyWeighmentsChart = await GetDailyWeighmentsChartData(),
                    MaterialSalesChart = await GetMaterialSalesChartData(),
                    CustomerOutstandingChart = await GetCustomerOutstandingChartData(),

                    // Alerts
                    LowStockMaterials = await GetLowStockMaterials(),
                    OverdueInvoices = await GetOverdueInvoices(),
                    PendingPayroll = new List<PendingPayrollData>(), // Stub implementation

                    // Quick Stats
                    AverageTransactionValue = await GetAverageTransactionValue(),
                    CollectionRate = await GetCollectionRate(),
                    EmployeeRetentionRate = await GetEmployeeRetentionRate()
                };

                return View(dashboardData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading report dashboard");
                return View(new ReportDashboardViewModel
                {
                    ErrorMessage = "An error occurred while loading dashboard data."
                });
            }
        }

        // GET: Report/Financial
        public async Task<IActionResult> Financial(DateTime? dateFrom, DateTime? dateTo, string reportType = "summary", string vatTypeFilter = "all")
        {
            try
            {
                var fromDate = dateFrom ?? DateTime.Today.AddMonths(-12);
                var toDate = dateTo ?? DateTime.Today;

                var viewModel = new FinancialReportViewModel
                {
                    DateFrom = fromDate,
                    DateTo = toDate,
                    ReportType = reportType,
                    VatTypeFilter = vatTypeFilter,
                    CompanyDetails = new ReportCompanyDetailsViewModel()
                };

                switch (reportType.ToLower())
                {
                    case "trialbalance":
                        viewModel.TrialBalance = await GenerateTrialBalance(fromDate, toDate, vatTypeFilter);
                        viewModel.ReportTitle = "Trial Balance" + VatFilterSuffix(vatTypeFilter);
                        break;
                    case "profitloss":
                        viewModel.ProfitLoss = await GenerateProfitLoss(fromDate, toDate, vatTypeFilter);
                        viewModel.ReportTitle = "Profit & Loss Statement" + VatFilterSuffix(vatTypeFilter);
                        break;
                    case "balancesheet":
                        viewModel.BalanceSheet = await GenerateBalanceSheet(toDate);
                        viewModel.ReportTitle = "Balance Sheet";
                        break;
                    case "cashflow":
                        viewModel.CashFlow = new CashFlowReport(); // Stub implementation
                        viewModel.ReportTitle = "Cash Flow Statement";
                        break;
                    default:
                        viewModel.FinancialSummary = new FinancialSummaryReport(); // Stub implementation
                        viewModel.ReportTitle = "Financial Summary";
                        break;
                }

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading financial report");
                return View(new FinancialReportViewModel
                {
                    ErrorMessage = "An error occurred while loading financial data."
                });
            }
        }

        // GET: Report/Operational
        public async Task<IActionResult> Operational(DateTime? dateFrom, DateTime? dateTo, string reportType = "summary")
        {
            try
            {
                var fromDate = dateFrom ?? DateTime.Today.AddMonths(-1);
                var toDate = dateTo ?? DateTime.Today;

                var viewModel = new OperationalReportViewModel
                {
                    DateFrom = fromDate,
                    DateTo = toDate,
                    ReportType = reportType
                };

                switch (reportType.ToLower())
                {
                    case "daily":
                        viewModel.DailyOperations = new DailyOperationsReport(); // Stub implementation
                        viewModel.ReportTitle = "Daily Operations Report";
                        break;
                    case "customer":
                        viewModel.CustomerReport = new CustomerReport(); // Stub implementation
                        viewModel.ReportTitle = "Customer Analysis Report";
                        break;
                    case "material":
                        viewModel.MaterialReport = new MaterialReport(); // Stub implementation
                        viewModel.ReportTitle = "Material Sales Report";
                        break;
                    case "vehicle":
                        viewModel.VehicleReport = new VehicleReport(); // Stub implementation
                        viewModel.ReportTitle = "Vehicle Analysis Report";
                        break;
                    default:
                        viewModel.OperationalSummary = await GenerateOperationalSummary(fromDate, toDate);
                        viewModel.ReportTitle = "Operational Summary";
                        break;
                }

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading operational report");
                return View(new OperationalReportViewModel
                {
                    ErrorMessage = "An error occurred while loading operational data."
                });
            }
        }

        // GET: Report/Tax
        public async Task<IActionResult> Tax(int? year, int? month, string reportType = "vat", string vatTypeFilter = "all")
        {
            try
            {
                var currentYear = year ?? DateTime.Today.Year;
                var currentMonth = month ?? DateTime.Today.Month;

                var viewModel = new TaxReportViewModel
                {
                    Year = currentYear,
                    Month = currentMonth,
                    ReportType = reportType,
                    VatTypeFilter = vatTypeFilter,
                    CompanyDetails = new ReportCompanyDetailsViewModel() // Stub implementation
                };

                switch (reportType.ToLower())
                {
                    case "vat":
                        viewModel.VATReport = await GenerateVATReport(currentYear, currentMonth, vatTypeFilter);
                        viewModel.ReportTitle = "VAT Report" + VatFilterSuffix(vatTypeFilter);
                        break;
                    case "paye":
                        viewModel.PAYEReport = await GeneratePAYEReport(currentYear, currentMonth);
                        viewModel.ReportTitle = "PAYE Report";
                        break;
                    case "pension":
                        viewModel.PensionReport = new PensionReport(); // Stub implementation
                        viewModel.ReportTitle = "Pension Report";
                        break;
                    case "compliance":
                        viewModel.ComplianceReport = new ComplianceReport(); // Stub implementation
                        viewModel.ReportTitle = "Tax Compliance Report";
                        break;
                    default:
                        viewModel.TaxSummary = new TaxSummaryReport(); // Stub implementation
                        viewModel.ReportTitle = "Tax Summary";
                        break;
                }

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading tax report");
                return View(new TaxReportViewModel
                {
                    ErrorMessage = "An error occurred while loading tax data."
                });
            }
        }

        // GET: Report/Stock
        public async Task<IActionResult> Stock(string reportType = "summary")
        {
            try
            {
                var viewModel = new StockReportViewModel
                {
                    ReportType = reportType
                };

                switch (reportType.ToLower())
                {
                    case "summary":
                        viewModel.StockSummary = await GenerateStockSummary();
                        viewModel.ReportTitle = "Stock Summary";
                        break;
                    case "movement":
                        viewModel.StockMovement = new StockMovementReport(); // Stub implementation
                        viewModel.ReportTitle = "Stock Movement Report";
                        break;
                    case "valuation":
                        viewModel.StockValuation = new StockValuationReport(); // Stub implementation
                        viewModel.ReportTitle = "Stock Valuation Report";
                        break;
                    case "reorder":
                        viewModel.ReorderReport = new ReorderReport(); // Stub implementation
                        viewModel.ReportTitle = "Reorder Report";
                        break;
                    default:
                        viewModel.StockSummary = await GenerateStockSummary();
                        viewModel.ReportTitle = "Stock Summary";
                        break;
                }

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading stock report");
                return View(new StockReportViewModel
                {
                    ErrorMessage = "An error occurred while loading stock data."
                });
            }
        }

        // GET: Report/Payroll
        public async Task<IActionResult> Payroll(int? year, int? month, string reportType = "summary")
        {
            try
            {
                var currentYear = year ?? DateTime.Today.Year;
                var currentMonth = month ?? DateTime.Today.Month;

                var viewModel = new PayrollReportViewModel
                {
                    Year = currentYear,
                    Month = currentMonth,
                    ReportType = reportType
                };

                switch (reportType.ToLower())
                {
                    case "summary":
                        viewModel.PayrollSummary = await GeneratePayrollSummary(currentYear, currentMonth);
                        viewModel.ReportTitle = "Payroll Summary";
                        break;
                    case "details":
                        viewModel.PayrollDetails = new PayrollDetailsReport(); // Stub implementation
                        viewModel.ReportTitle = "Payroll Details Report";
                        break;
                    case "compliance":
                        viewModel.ComplianceReport = new ComplianceReport(); // Stub implementation
                        viewModel.ReportTitle = "Payroll Compliance Report";
                        break;
                    case "bank":
                        viewModel.BankReport = new BankPaymentReport(); // Stub implementation
                        viewModel.ReportTitle = "Bank Payment Report";
                        break;
                    default:
                        viewModel.PayrollSummary = await GeneratePayrollSummary(currentYear, currentMonth);
                        viewModel.ReportTitle = "Payroll Summary";
                        break;
                }

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading payroll report");
                return View(new PayrollReportViewModel
                {
                    ErrorMessage = "An error occurred while loading payroll data."
                });
            }
        }

        // GET: Report/Export/Excel
        public async Task<IActionResult> ExportExcel(string reportType, DateTime? dateFrom, DateTime? dateTo)
        {
            try
            {
                var fromDate = dateFrom ?? DateTime.Today.AddMonths(-1);
                var toDate = dateTo ?? DateTime.Today;

                byte[] excelFile;
                string fileName;

                switch (reportType.ToLower())
                {
                    case "dailyoperations":
                        excelFile = await ExportDailyOperationsExcel(fromDate, toDate);
                        fileName = $"Daily_Operations_{fromDate:yyyyMMdd}_to_{toDate:yyyyMMdd}.xlsx";
                        break;
                    case "customeroutstanding":
                        excelFile = new byte[0]; // Stub implementation
                        fileName = $"Customer_Outstanding_{DateTime.Now:yyyyMMdd}.xlsx";
                        break;
                    case "payrollsummary":
                        excelFile = new byte[0]; // Stub implementation
                        fileName = $"Payroll_Summary_{fromDate:yyyyMM}_to_{toDate:yyyyMM}.xlsx";
                        break;
                    default:
                        excelFile = await ExportDailyOperationsExcel(fromDate, toDate);
                        fileName = $"Operations_Report_{fromDate:yyyyMMdd}_to_{toDate:yyyyMMdd}.xlsx";
                        break;
                }

                return File(excelFile, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting Excel report");
                return BadRequest("Error generating Excel file");
            }
        }

        // GET: Report/Export/PDF
        public async Task<IActionResult> ExportPDF(string reportType, int? id, DateTime? dateFrom, DateTime? dateTo)
        {
            try
            {
                byte[] pdfFile;
                string fileName;

                switch (reportType.ToLower())
                {
                    case "invoice":
                        if (!id.HasValue) return BadRequest("Invoice ID required");
                        pdfFile = await ExportInvoicePDF(id.Value);
                        fileName = $"Invoice_{id.Value}_{DateTime.Now:yyyyMMdd}.pdf";
                        break;
                    case "payslip":
                        if (!id.HasValue) return BadRequest("Payslip ID required");
                        pdfFile = new byte[0]; // Stub implementation
                        fileName = $"Payslip_{id.Value}_{DateTime.Now:yyyyMMdd}.pdf";
                        break;
                    case "trialbalance":
                        var fromDate = dateFrom ?? DateTime.Today.AddMonths(-1);
                        var toDate = dateTo ?? DateTime.Today;
                        pdfFile = new byte[0]; // Stub implementation
                        fileName = $"Trial_Balance_{fromDate:yyyyMMdd}_to_{toDate:yyyyMMdd}.pdf";
                        break;
                    default:
                        return BadRequest("Invalid report type");
                }

                return File(pdfFile, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting PDF report");
                return BadRequest("Error generating PDF file");
            }
        }

        // Helper methods for report generation
        private async Task<List<MonthlyRevenueData>> GetMonthlyRevenueChartData()
        {
            var endDate = DateTime.Today;
            var startDate = endDate.AddMonths(-11);

            var data = new List<MonthlyRevenueData>();

            for (var date = startDate; date <= endDate; date = date.AddMonths(1))
            {
                var monthStart = new DateTime(date.Year, date.Month, 1);
                var monthEnd = monthStart.AddMonths(1).AddDays(-1);

                var revenue = await _context.Invoices
                    .Where(i => i.InvoiceDate >= monthStart && i.InvoiceDate <= monthEnd && i.Status == "Paid")
                    .SumAsync(i => i.PaidAmount);

                data.Add(new MonthlyRevenueData
                {
                    Month = monthStart.ToString("MMM yyyy"),
                    Revenue = revenue
                });
            }

            return data;
        }

        private async Task<List<DailyWeighmentData>> GetDailyWeighmentsChartData()
        {
            var endDate = DateTime.Today;
            var startDate = endDate.AddDays(-29);

            var data = new List<DailyWeighmentData>();

            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                var count = await _context.WeighmentTransactions
                    .CountAsync(w => w.TransactionDate.Date == date);

                var totalWeight = await _context.WeighmentTransactions
                    .Where(w => w.TransactionDate.Date == date)
                    .SumAsync(w => w.NetWeight);

                data.Add(new DailyWeighmentData
                {
                    Date = date.ToString("dd/MM"),
                    Count = count,
                    TotalWeight = totalWeight
                });
            }

            return data;
        }

        private async Task<List<MaterialSalesChartData>> GetMaterialSalesChartData()
        {
            var data = await _context.WeighmentTransactions
                .Include(w => w.Material)
                .Where(w => w.Status == "Completed" && w.Material != null)
                .GroupBy(w => w.Material!.Name)
                .Select(g => new MaterialSalesChartData
                {
                    MaterialName = g.Key,
                    TotalQuantity = g.Sum(w => w.NetWeight),
                    TotalRevenue = g.Sum(w => w.TotalAmount ?? 0)
                })
                .OrderByDescending(m => m.TotalRevenue)
                .Take(10)
                .ToListAsync();

            return data;
        }

        private async Task<List<CustomerOutstandingData>> GetCustomerOutstandingChartData()
        {
            var data = await _context.Customers
                .Where(c => c.Status == "Active" && c.OutstandingBalance > 0)
                .OrderByDescending(c => c.OutstandingBalance)
                .Take(10)
                .Select(c => new CustomerOutstandingData
                {
                    CustomerName = c.Name,
                    OutstandingBalance = c.OutstandingBalance,
                    CreditLimit = c.CreditLimit
                })
                .ToListAsync();

            return data;
        }

        private async Task<List<LowStockMaterialData>> GetLowStockMaterials()
        {
            var data = await _context.Materials
                .Include(m => m.StockYards)
                .Where(m => m.Status == "Active")
                .Select(m => new
                {
                    Material = m,
                    TotalStock = m.StockYards.Sum(sy => sy.CurrentStock),
                    AvailableStock = m.StockYards.Sum(sy => sy.AvailableStock)
                })
                .Where(m => m.AvailableStock < 50) // Low stock threshold
                .Select(m => new LowStockMaterialData
                {
                    MaterialName = m.Material.Name,
                    CurrentStock = m.TotalStock,
                    AvailableStock = m.AvailableStock,
                    MinimumStock = 50
                })
                .ToListAsync();

            return data;
        }

        private async Task<List<OverdueInvoiceData>> GetOverdueInvoices()
        {
            var data = await _context.Invoices
                .Include(i => i.Customer)
                .Where(i => i.Status == "Overdue" && i.DueDate.HasValue)
                .OrderByDescending(i => i.TotalAmount - i.PaidAmount)
                .Take(10)
                .Select(i => new OverdueInvoiceData
                {
                    InvoiceNumber = i.InvoiceNumber,
                    CustomerName = i.Customer.Name,
                    OutstandingAmount = (i.TotalAmount - i.PaidAmount),
                    DaysOverdue = (DateTime.Now - i.DueDate!.Value).Days
                })
                .ToListAsync();

            return data;
        }

        private async Task<decimal> GetAverageTransactionValue()
        {
            var avg = await _context.WeighmentTransactions
                .Where(w => w.Status == "Completed" && w.TotalAmount.HasValue)
                .AverageAsync(w => (decimal?)w.TotalAmount) ?? 0m;

            return avg;
        }

        private async Task<double> GetCollectionRate()
        {
            var totalInvoiced = await _context.Invoices.SumAsync(i => i.TotalAmount);
            var totalPaid = await _context.Invoices.SumAsync(i => i.PaidAmount);

            return totalInvoiced > 0 ? (double)(totalPaid / totalInvoiced) : 0;
        }

        private async Task<double> GetEmployeeRetentionRate()
        {
            var totalEmployees = await _context.Employees.CountAsync();
            var activeEmployees = await _context.Employees.CountAsync(e => e.Status == "Active");

            return totalEmployees > 0 ? (double)activeEmployees / totalEmployees : 0;
        }

        // ==================================================================
        // VAT-TYPE FILTERING HELPERS
        // ==================================================================
        // Reports below aggregate from JournalEntryLines (not from
        // ChartOfAccounts.CurrentBalance) so the date range is honored AND
        // the VAT-type filter can be applied.
        //
        // The filter works at the JournalEntry level, not the line level.
        // For each entry we look at its lines, find the customer-attributable
        // ones (account codes starting with "1101-" for receivables and
        // "2103-" for prepayment liabilities), parse the customer ID from the
        // suffix, and look up that customer's VAT type. The whole entry is
        // then either In or Out of the filtered view — keeping debits = credits
        // within the filter, which is the only honest way to do it.
        //
        // Entries with NO customer leg (rent, payroll, depreciation, etc.)
        // are excluded from filtered views entirely — they have no VAT type
        // to attribute them to. They reappear in the unfiltered "All" view.
        // ==================================================================

        /// <summary>
        /// Returns the suffix shown in report titles to make the active filter visible.
        /// </summary>
        private static string VatFilterSuffix(string vatTypeFilter)
        {
            return string.Equals(vatTypeFilter, "all", StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : $" — {vatTypeFilter} customers only";
        }

        /// <summary>
        /// Builds a set of JournalEntry IDs that match the requested VAT-type
        /// filter for the date range. Returns null when the filter is "all"
        /// (caller should treat null as "no filter, include everything").
        /// </summary>
        private async Task<HashSet<int>?> GetEntryIdsMatchingVatTypeAsync(
            DateTime fromDate, DateTime toDate, string vatTypeFilter)
        {
            if (string.Equals(vatTypeFilter, "all", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            // Build a CustomerId → VatTypeName map once. Reading all customers
            // is fine — it's a tiny table compared with journal lines, and
            // doing it via JOIN inside the line query gets ugly because the
            // CustomerId is encoded in the account code, not stored as a FK.
            var customerVatMap = await _context.Customers
                .Include(c => c.VatType)
                .Select(c => new { c.Id, VatTypeName = c.VatType != null ? c.VatType.Name : null })
                .ToListAsync();

            // Match by Contains so labels like "Exclusive(Paid By Customer)"
            // and "Inclusive(Paid By Quarry)" still classify correctly.
            var matchingCustomerIds = customerVatMap
                .Where(c => !string.IsNullOrEmpty(c.VatTypeName)
                            && c.VatTypeName!.IndexOf(vatTypeFilter, StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(c => c.Id)
                .ToHashSet();

            if (matchingCustomerIds.Count == 0)
            {
                // No customers of this VAT type → nothing to include.
                return new HashSet<int>();
            }

            // Pull all entry lines in the date range, with the matching account
            // codes only. Anything starting with 1101- (A/R) or 2103-
            // (prepayments) carries a customer ID in its suffix.
            var customerLines = await _context.JournalEntryLines
                .Include(l => l.Account)
                .Include(l => l.JournalEntry)
                .Where(l => l.JournalEntry.EntryDate >= fromDate
                         && l.JournalEntry.EntryDate <= toDate)
                .Where(l => l.Account.AccountCode.StartsWith("1101-")
                         || l.Account.AccountCode.StartsWith("2103-"))
                .Select(l => new { l.JournalEntryId, l.Account.AccountCode })
                .ToListAsync();

            var matchingEntryIds = new HashSet<int>();
            foreach (var line in customerLines)
            {
                // Account code shape: "1101-NNNNNN" or "2103-NNNNNN".
                var dashIdx = line.AccountCode.IndexOf('-');
                if (dashIdx < 0 || dashIdx == line.AccountCode.Length - 1) continue;
                var suffix = line.AccountCode.Substring(dashIdx + 1);
                if (!int.TryParse(suffix, out var customerId)) continue;
                if (matchingCustomerIds.Contains(customerId))
                {
                    matchingEntryIds.Add(line.JournalEntryId);
                }
            }

            return matchingEntryIds;
        }

        // Financial Report Generation Methods
        private async Task<TrialBalanceReport> GenerateTrialBalance(
            DateTime fromDate, DateTime toDate, string vatTypeFilter = "all")
        {
            // Find which entries are in scope for the filter (null = all).
            var allowedEntryIds = await GetEntryIdsMatchingVatTypeAsync(fromDate, toDate, vatTypeFilter);

            // Aggregate per account from JournalEntryLines, honoring the date
            // range. Compared with reading ChartOfAccounts.CurrentBalance,
            // this fixes a long-standing bug: previously TB ignored its date
            // filter and always returned current-balance figures.
            var lineQuery = _context.JournalEntryLines
                .Include(l => l.Account)
                .Include(l => l.JournalEntry)
                .Where(l => l.JournalEntry.EntryDate >= fromDate
                         && l.JournalEntry.EntryDate <= toDate)
                .Where(l => l.Account.IsActive);

            if (allowedEntryIds != null)
            {
                if (allowedEntryIds.Count == 0)
                {
                    // Filter chosen but no entries match. Return an empty TB
                    // rather than something misleading.
                    return new TrialBalanceReport
                    {
                        FromDate = fromDate,
                        ToDate = toDate,
                        Accounts = new List<TrialBalanceAccount>(),
                        TotalDebit = 0,
                        TotalCredit = 0
                    };
                }
                lineQuery = lineQuery.Where(l => allowedEntryIds.Contains(l.JournalEntryId));
            }

            var perAccount = await lineQuery
                .GroupBy(l => new { l.AccountId, l.Account.AccountCode, l.Account.AccountName, l.Account.AccountType })
                .Select(g => new
                {
                    g.Key.AccountCode,
                    g.Key.AccountName,
                    g.Key.AccountType,
                    Debit = g.Sum(l => l.DebitAmount),
                    Credit = g.Sum(l => l.CreditAmount)
                })
                .ToListAsync();

            // Convert per-account net activity into the trial-balance two-column
            // format. Asset/Expense are debit-natural — a positive net (more
            // debits than credits) lands in the Debit column; a negative net
            // means the account ran as a contra and lands in Credit. The
            // mirror applies to liability/equity/revenue accounts. Rows that
            // netted to zero in the period are dropped.
            var accounts = perAccount
                .Select(a =>
                {
                    var net = a.Debit - a.Credit;
                    var isDebitNatural = a.AccountType == "Asset" || a.AccountType == "Expense";
                    decimal dr = 0, cr = 0;
                    if (isDebitNatural)
                    {
                        if (net >= 0) dr = net; else cr = -net;
                    }
                    else
                    {
                        if (net <= 0) cr = -net; else dr = net;
                    }
                    return new TrialBalanceAccount
                    {
                        AccountCode = a.AccountCode,
                        AccountName = a.AccountName,
                        AccountType = a.AccountType,
                        DebitBalance = dr,
                        CreditBalance = cr
                    };
                })
                .Where(a => a.DebitBalance != 0 || a.CreditBalance != 0)
                .OrderBy(a => a.AccountCode)
                .ToList();

            return new TrialBalanceReport
            {
                FromDate = fromDate,
                ToDate = toDate,
                Accounts = accounts,
                TotalDebit = accounts.Sum(a => a.DebitBalance),
                TotalCredit = accounts.Sum(a => a.CreditBalance)
            };
        }

        private async Task<ProfitLossReport> GenerateProfitLoss(
            DateTime fromDate, DateTime toDate, string vatTypeFilter = "all")
        {
            var allowedEntryIds = await GetEntryIdsMatchingVatTypeAsync(fromDate, toDate, vatTypeFilter);

            var lineQuery = _context.JournalEntryLines
                .Include(l => l.Account)
                .Include(l => l.JournalEntry)
                .Where(l => l.JournalEntry.EntryDate >= fromDate
                         && l.JournalEntry.EntryDate <= toDate)
                .Where(l => l.Account.IsActive)
                .Where(l => l.Account.AccountType == "Revenue" || l.Account.AccountType == "Expense");

            if (allowedEntryIds != null)
            {
                if (allowedEntryIds.Count == 0)
                {
                    return new ProfitLossReport
                    {
                        FromDate = fromDate,
                        ToDate = toDate,
                        Revenue = 0,
                        Expenses = 0,
                        NetProfit = 0
                    };
                }
                lineQuery = lineQuery.Where(l => allowedEntryIds.Contains(l.JournalEntryId));
            }

            var rows = await lineQuery
                .GroupBy(l => l.Account.AccountType)
                .Select(g => new { Type = g.Key, Debit = g.Sum(l => l.DebitAmount), Credit = g.Sum(l => l.CreditAmount) })
                .ToListAsync();

            // Revenue accounts are credit-natural: revenue total = credits − debits.
            // Expense accounts are debit-natural: expense total = debits − credits.
            var revenueRow = rows.FirstOrDefault(r => r.Type == "Revenue");
            var expenseRow = rows.FirstOrDefault(r => r.Type == "Expense");

            var revenue = revenueRow != null ? revenueRow.Credit - revenueRow.Debit : 0m;
            var expenses = expenseRow != null ? expenseRow.Debit - expenseRow.Credit : 0m;

            return new ProfitLossReport
            {
                FromDate = fromDate,
                ToDate = toDate,
                Revenue = revenue,
                Expenses = expenses,
                NetProfit = revenue - expenses
            };
        }

        private async Task<BalanceSheetReport> GenerateBalanceSheet(DateTime asOfDate)
        {
            var assets = await _context.ChartOfAccounts
                .Where(ca => ca.AccountType == "Asset" && ca.IsActive)
                .ToListAsync();

            var liabilities = await _context.ChartOfAccounts
                .Where(ca => ca.AccountType == "Liability" && ca.IsActive)
                .ToListAsync();

            var equity = await _context.ChartOfAccounts
                .Where(ca => ca.AccountType == "Equity" && ca.IsActive)
                .ToListAsync();

            var balanceSheet = new BalanceSheetReport
            {
                AsOfDate = asOfDate,
                TotalAssets = assets.Sum(ca => ca.CurrentBalance),
                TotalLiabilities = liabilities.Sum(ca => ca.CurrentBalance),
                TotalEquity = equity.Sum(ca => ca.CurrentBalance)
            };

            return balanceSheet;
        }

        // Operational Report Generation Methods
        private async Task<OperationalSummaryReport> GenerateOperationalSummary(DateTime fromDate, DateTime toDate)
        {
            var weighments = await _context.WeighmentTransactions
                .Where(w => w.TransactionDate >= fromDate && w.TransactionDate <= toDate && w.Status == "Completed")
                .ToListAsync();

            var summary = new OperationalSummaryReport
            {
                FromDate = fromDate,
                ToDate = toDate,
                TotalTransactions = weighments.Count,
                TotalWeight = weighments.Sum(w => w.NetWeight),
                TotalRevenue = weighments.Sum(w => w.TotalAmount ?? 0),
                AverageTransactionValue = weighments.Any() ? weighments.Average(w => w.TotalAmount ?? 0) : 0,
                TopMaterials = new List<TopMaterialData>(), // Stub implementation
                TopCustomers = new List<TopCustomerData>() // Stub implementation
            };

            return summary;
        }

        // Tax Report Generation Methods
        private async Task<VATReport> GenerateVATReport(int year, int month, string vatTypeFilter = "all")
        {
            var startDate = new DateTime(year, month, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            var vatReport = new VATReport
            {
                Year = year,
                Month = month,
                Period = startDate.ToString("MMMM yyyy")
            };

            // Pull invoices for the period, with the customer's VAT type so we
            // can apply the filter. Substring match on the VatType.Name keeps
            // labels like "Exclusive(Paid By Customer)" classified correctly.
            var query = _context.Invoices
                .Include(i => i.Customer)
                    .ThenInclude(c => c!.VatType)
                .Where(i => i.InvoiceDate >= startDate && i.InvoiceDate <= endDate);

            var salesInvoices = await query.ToListAsync();

            if (!string.Equals(vatTypeFilter, "all", StringComparison.OrdinalIgnoreCase))
            {
                salesInvoices = salesInvoices
                    .Where(i => i.Customer?.VatType?.Name != null
                                && i.Customer.VatType.Name.IndexOf(vatTypeFilter, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();
            }

            vatReport.OutputVAT = salesInvoices.Sum(i => i.VatAmount);

            // VAT on Purchases (Input VAT) - would need purchase data
            vatReport.InputVAT = 0; // Implement when purchase module is added

            vatReport.NetVAT = vatReport.OutputVAT - vatReport.InputVAT;
            vatReport.VATPayable = Math.Max(0, vatReport.NetVAT);
            vatReport.VATReceivable = Math.Max(0, -vatReport.NetVAT);

            return vatReport;
        }

        private async Task<PAYEReport> GeneratePAYEReport(int year, int month)
        {
            var startDate = new DateTime(year, month, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            var payrollRuns = await _context.PayrollRuns
                .Include(pr => pr.EmployeeSalaries)
                .Where(pr => pr.PaymentMonth.Year == year && pr.PaymentMonth.Month == month && pr.Status == "Processed")
                .ToListAsync();

            var payeReport = new PAYEReport
            {
                Year = year,
                Month = month,
                Period = startDate.ToString("MMMM yyyy"),
                TotalPAYE = payrollRuns.Sum(pr => pr.TotalPAYE),
                EmployeeCount = payrollRuns.Sum(pr => pr.TotalEmployees),
                MonthlyRemittance = payrollRuns.Sum(pr => pr.TotalPAYE),
                DueDate = startDate.AddMonths(1).AddDays(10) // 10th of next month
            };

            return payeReport;
        }

        // Stock Report Generation Methods
        private async Task<StockSummaryReport> GenerateStockSummary()
        {
            var materials = await _context.Materials
                .Include(m => m.StockYards)
                .Where(m => m.Status == "Active")
                .ToListAsync();

            var summary = new StockSummaryReport
            {
                TotalMaterials = materials.Count,
                TotalStockValue = 0, // Calculate based on current stock and prices
                LowStockCount = 0,
                OutOfStockCount = 0,
                StockItems = materials.Select(m => new StockItem
                {
                    MaterialName = m.Name,
                    MaterialType = m.Type,
                    CurrentStock = m.StockYards.Sum(sy => sy.CurrentStock),
                    ReservedStock = m.StockYards.Sum(sy => sy.ReservedStock),
                    AvailableStock = m.StockYards.Sum(sy => sy.AvailableStock),
                    UnitPrice = m.UnitPrice,
                    StockValue = m.StockYards.Sum(sy => sy.AvailableStock) * m.UnitPrice,
                    Status = m.StockYards.Sum(sy => sy.AvailableStock) <= 0 ? "Out of Stock" :
                            m.StockYards.Sum(sy => sy.AvailableStock) < 50 ? "Low Stock" : "In Stock"
                }).ToList()
            };

            summary.TotalStockValue = summary.StockItems.Sum(i => i.StockValue);
            summary.LowStockCount = summary.StockItems.Count(i => i.Status == "Low Stock");
            summary.OutOfStockCount = summary.StockItems.Count(i => i.Status == "Out of Stock");

            return summary;
        }

        // Payroll Report Generation Methods
        private async Task<PayrollSummaryReport> GeneratePayrollSummary(int year, int month)
        {
            var payrollRuns = await _context.PayrollRuns
                .Include(pr => pr.EmployeeSalaries)
                .ThenInclude(es => es.Employee)
                .Where(pr => pr.PaymentMonth.Year == year && pr.PaymentMonth.Month == month && pr.Status == "Processed")
                .ToListAsync();

            var summary = new PayrollSummaryReport
            {
                Year = year,
                Month = month,
                Period = new DateTime(year, month, 1).ToString("MMMM yyyy"),
                TotalEmployees = payrollRuns.Sum(pr => pr.TotalEmployees),
                GrossPay = payrollRuns.Sum(pr => pr.GrossPay),
                NetPay = payrollRuns.Sum(pr => pr.NetPay),
                TotalPAYE = payrollRuns.Sum(pr => pr.TotalPAYE),
                TotalPension = payrollRuns.Sum(pr => pr.TotalPension),
                TotalNHIS = payrollRuns.Sum(pr => pr.TotalNHIS),
                EmployerContributions = payrollRuns.Sum(pr => pr.EmployeeSalaries.Sum(es => es.GetTotalEmployerContributions()))
            };

            return summary;
        }

        // Export Methods
        private async Task<byte[]> ExportDailyOperationsExcel(DateTime fromDate, DateTime toDate)
        {
            var weighments = await _context.WeighmentTransactions
                .Include(w => w.Customer)
                .Include(w => w.Material)
                .Where(w => w.TransactionDate >= fromDate && w.TransactionDate <= toDate && w.Status == "Completed")
                .OrderBy(w => w.TransactionDate)
                .ToListAsync();

            // Use ClosedXML to create Excel file
            // Implementation would go here
            return new byte[0]; // Placeholder
        }

        private async Task<byte[]> ExportInvoicePDF(int invoiceId)
        {
            var invoice = await _context.Invoices
                .Include(i => i.Customer)
                .Include(i => i.WeighmentTransaction)
                .ThenInclude(w => w.Material)
                .FirstOrDefaultAsync(i => i.Id == invoiceId);

            if (invoice == null) return new byte[0];

            // Use QuestPDF to create PDF
            // Implementation would go here
            return new byte[0]; // Placeholder
        }

        private CompanyDetailsViewModel GetCompanyDetails()
        {
            return new CompanyDetailsViewModel
            {
                CompanyName = "Nigerian Quarry Management System",
                Address = "123 Quarry Road, Industrial Estate",
                City = "Lagos",
                State = "Lagos",
                Phone = "+234-1-2345678",
                Email = "info@quarry.ng",
                Website = "www.quarry.ng",
                TaxNumber = "12345678-0001",
                BankDetails = "Access Bank, Account: 1234567890"
            };
        }
    }
}