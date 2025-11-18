using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuarryManagementSystem.Data;
using QuarryManagementSystem.Models.Domain;

namespace QuarryManagementSystem.Controllers
{
    [Authorize(Roles = "Admin,Manager,Accountant")]
    public class ChartOfAccountsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ChartOfAccountsController> _logger;

        public ChartOfAccountsController(ApplicationDbContext context, ILogger<ChartOfAccountsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: ChartOfAccounts
        public async Task<IActionResult> Index()
        {
            var accounts = await _context.ChartOfAccounts
                .OrderBy(a => a.AccountCode)
                .ToListAsync();

            return View(accounts);
        }

        // GET: ChartOfAccounts/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var account = await _context.ChartOfAccounts
                .FirstOrDefaultAsync(m => m.Id == id);

            if (account == null)
            {
                return NotFound();
            }

            return View(account);
        }
    }
}