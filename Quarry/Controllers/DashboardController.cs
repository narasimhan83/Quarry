using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuarryManagementSystem.Data;
using QuarryManagementSystem.Models;
using QuarryManagementSystem.ViewModels;

namespace QuarryManagementSystem.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<DashboardController> _logger;

        public DashboardController(ApplicationDbContext context, ILogger<DashboardController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var today = DateTime.Today;
                var thisMonth = new DateTime(today.Year, today.Month, 1);
                var lastMonth = thisMonth.AddMonths(-1);

                // Get current user
                var currentUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.UserName == User.Identity!.Name);

                // Dashboard statistics
                var dashboardData = new DashboardViewModel
                {
                // Today's statistics
                //
                // These are based on CreatedAt (when the record was saved), NOT
                // TransactionDate (the operator-visible "date of the weighment").
                // Rationale: TransactionDate is editable on the Create form —
                // an operator who saves a backdated slip to cover yesterday's
                // unrecorded delivery would otherwise see their KPI stay at 0
                // even though they just did work today. "Today's Weighments"
                // should reflect activity, not document-dating.
                TodayWeighments = await _context.WeighmentTransactions
                    .CountAsync(w => w.CreatedAt >= today && w.CreatedAt < today.AddDays(1)),

                TodayRevenue = await _context.WeighmentTransactions
                    .Where(w => w.CreatedAt >= today && w.CreatedAt < today.AddDays(1) && w.TotalAmount.HasValue)
                    .SumAsync(w => w.TotalAmount.Value),

                // Monthly statistics (same rationale: CreatedAt, not TransactionDate).
                MonthlyWeighments = await _context.WeighmentTransactions
                    .CountAsync(w => w.CreatedAt >= thisMonth),

                MonthlyRevenue = await _context.WeighmentTransactions
                    .Where(w => w.CreatedAt >= thisMonth && w.TotalAmount.HasValue)
                    .SumAsync(w => w.TotalAmount.Value),

                    // Customer statistics
                    TotalCustomers = await _context.Customers
                        .CountAsync(c => c.Status == "Active"),
                    
                    ActiveCustomers = await _context.Customers
                        .CountAsync(c => c.Status == "Active" && c.WeighmentTransactions.Any(w => w.TransactionDate >= thisMonth)),

                    // Material statistics
                    TotalMaterials = await _context.Materials
                        .CountAsync(m => m.Status == "Active"),

                    // Invoice statistics
                    TotalInvoices = await _context.Invoices.CountAsync(),
                    UnpaidInvoices = await _context.Invoices
                        .CountAsync(i => i.Status == "Unpaid" || i.Status == "Overdue"),
                    
                    OverdueInvoices = await _context.Invoices
                        .CountAsync(i => i.Status == "Overdue"),

                    OutstandingAmount = await _context.Invoices
                        .Where(i => i.Status == "Unpaid" || i.Status == "Overdue")
                        .SumAsync(i => i.TotalAmount - i.PaidAmount),

                    // Recent transactions
                    RecentTransactions = await _context.WeighmentTransactions
                        .Include(w => w.Customer)
                        .Include(w => w.Material)
                        .Where(w => w.Status == "Completed")
                        .OrderByDescending(w => w.TransactionDate)
                        .Take(10)
                        .Select(w => new RecentTransactionViewModel
                        {
                            Id = w.Id,
                            TransactionNumber = w.TransactionNumber,
                            TransactionDate = w.TransactionDate,
                            CustomerName = w.Customer != null ? w.Customer.Name : "Walk-in Customer",
                            MaterialName = w.Material != null ? w.Material.Name : "Unknown",
                            VehicleRegNumber = w.VehicleRegNumber,
                            NetWeight = w.NetWeight,
                            TotalAmount = w.TotalAmount ?? 0,
                            Status = w.Status
                        })
                        .ToListAsync(),

                    // Low stock alerts
                    LowStockMaterials = await _context.Materials
                        .Where(m => m.Status == "Active")
                        .Select(m => new MaterialStockViewModel
                        {
                            Id = m.Id,
                            Name = m.Name,
                            Type = m.Type,
                            UnitPrice = m.UnitPrice,
                            CurrentStock = m.StockYards.Sum(sy => sy.CurrentStock),
                            MinimumStock = 50 // Default minimum stock level
                        })
                        .Where(m => m.CurrentStock < m.MinimumStock)
                        .ToListAsync(),

                    // Monthly trend data
                    MonthlyTrend = await GetMonthlyTrendData(),

                    // User information
                    CurrentUser = currentUser != null ? new UserViewModel
                    {
                        Id = currentUser.Id,
                        FullName = currentUser.FullName,
                        Email = currentUser.Email!,
                        Role = User.IsInRole("Admin") ? "Administrator" : 
                               User.IsInRole("Manager") ? "Manager" :
                               User.IsInRole("Accountant") ? "Accountant" :
                               User.IsInRole("Operator") ? "Operator" : "Viewer",
                        LastLogin = currentUser.LastLogin
                    } : null
                };

                // Log dashboard access
                _logger.LogInformation("User {UserName} accessed dashboard at {Time}", 
                    User.Identity!.Name, DateTime.Now);

                return View(dashboardData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading dashboard data for user {UserName}", User.Identity!.Name);
                return View(new DashboardViewModel
                {
                    ErrorMessage = "An error occurred while loading dashboard data. Please try again."
                });
            }
        }

        private async Task<List<MonthlyTrendData>> GetMonthlyTrendData()
        {
            var endDate = DateTime.Today;
            var startDate = endDate.AddMonths(-11);
            
            var trendData = new List<MonthlyTrendData>();

            for (var date = startDate; date <= endDate; date = date.AddMonths(1))
            {
                var monthStart = new DateTime(date.Year, date.Month, 1);
                var monthEnd = monthStart.AddMonths(1).AddDays(-1);

                var weighments = await _context.WeighmentTransactions
                    .Where(w => w.TransactionDate >= monthStart && w.TransactionDate <= monthEnd && w.TotalAmount.HasValue)
                    .SumAsync(w => w.TotalAmount.Value);

                var invoices = await _context.Invoices
                    .Where(i => i.InvoiceDate >= monthStart && i.InvoiceDate <= monthEnd)
                    .SumAsync(i => i.TotalAmount);

                trendData.Add(new MonthlyTrendData
                {
                    Month = monthStart.ToString("MMM yyyy"),
                    WeighmentRevenue = weighments,
                    InvoiceAmount = invoices,
                    TransactionCount = await _context.WeighmentTransactions
                        .CountAsync(w => w.TransactionDate >= monthStart && w.TransactionDate <= monthEnd)
                });
            }

            return trendData;
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = HttpContext.TraceIdentifier });
        }

        // AJAX endpoints for dashboard widgets
        [HttpGet]
        public async Task<JsonResult> GetTodayStats()
        {
            try
            {
                var today = DateTime.Today;
                
                var stats = new
                {
                    // Same rule as DashboardViewModel above: count by CreatedAt
                    // so activity today shows up even for backdated slips.
                    weighments = await _context.WeighmentTransactions
                        .CountAsync(w => w.CreatedAt >= today && w.CreatedAt < today.AddDays(1)),
                    revenue = await _context.WeighmentTransactions
                        .Where(w => w.CreatedAt >= today && w.CreatedAt < today.AddDays(1) && w.TotalAmount.HasValue)
                        .SumAsync(w => w.TotalAmount.Value),
                    pendingInvoices = await _context.Invoices
                        .CountAsync(i => i.Status == "Unpaid"),
                    overdueInvoices = await _context.Invoices
                        .CountAsync(i => i.Status == "Overdue")
                };

                return Json(new { success = true, data = stats });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting today's stats");
                return Json(new { success = false, message = "Error retrieving statistics" });
            }
        }

        [HttpGet]
        public async Task<JsonResult> GetMonthlyChartData()
        {
            try
            {
                var data = await GetMonthlyTrendData();
                return Json(new { success = true, data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting monthly chart data");
                return Json(new { success = false, message = "Error retrieving chart data" });
            }
        }

        [HttpGet]
        public async Task<JsonResult> GetRecentTransactions()
        {
            try
            {
                var transactions = await _context.WeighmentTransactions
                    .Include(w => w.Customer)
                    .Include(w => w.Material)
                    .Where(w => w.Status == "Completed")
                    .OrderByDescending(w => w.TransactionDate)
                    .Take(5)
                    .Select(w => new
                    {
                        transactionNumber = w.TransactionNumber,
                        customerName = w.Customer != null ? w.Customer.Name : "Walk-in Customer",
                        materialName = w.Material != null ? w.Material.Name : "Unknown",
                        vehicleRegNumber = w.VehicleRegNumber,
                        netWeight = w.NetWeight,
                        totalAmount = w.TotalAmount ?? 0,
                        transactionDate = w.TransactionDate.ToString("dd/MM/yyyy HH:mm")
                    })
                    .ToListAsync();

                return Json(new { success = true, data = transactions });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recent transactions");
                return Json(new { success = false, message = "Error retrieving transactions" });
            }
        }
    }
}