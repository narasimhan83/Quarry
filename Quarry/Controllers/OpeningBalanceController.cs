using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuarryManagementSystem.Data;
using QuarryManagementSystem.Models.Domain;

namespace QuarryManagementSystem.Controllers
{
    [Authorize(Roles = "Admin,Manager,Accountant")]
    public class OpeningBalanceController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<OpeningBalanceController> _logger;

        public OpeningBalanceController(ApplicationDbContext context, ILogger<OpeningBalanceController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: OpeningBalance
        // Read-only view of opening and current balances per account
        public async Task<IActionResult> Index()
        {
            var accounts = await _context.ChartOfAccounts
                .OrderBy(a => a.AccountCode)
                .ToListAsync();

            return View(accounts);
        }
    }
}