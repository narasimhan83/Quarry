using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuarryManagementSystem.Data;
using QuarryManagementSystem.Models.Domain;

namespace QuarryManagementSystem.Controllers
{
    [Authorize(Roles = "Admin,Manager,Accountant")]
    public class JournalEntryController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<JournalEntryController> _logger;

        public JournalEntryController(ApplicationDbContext context, ILogger<JournalEntryController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: JournalEntry
        public async Task<IActionResult> Index(DateTime? fromDate, DateTime? toDate)
        {
            var query = _context.JournalEntries
                .Include(je => je.JournalEntryLines)
                    .ThenInclude(jel => jel.Account)
                .OrderByDescending(je => je.EntryDate)
                .ThenByDescending(je => je.Id)
                .AsQueryable();

            var from = fromDate ?? DateTime.Today.AddMonths(-1);
            var to = toDate ?? DateTime.Today;

            query = query.Where(je => je.EntryDate.Date >= from.Date && je.EntryDate.Date <= to.Date);

            ViewBag.FromDate = from.ToString("yyyy-MM-dd");
            ViewBag.ToDate = to.ToString("yyyy-MM-dd");

            var entries = await query.ToListAsync();
            return View(entries);
        }

        // GET: JournalEntry/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var entry = await _context.JournalEntries
                .Include(je => je.JournalEntryLines)
                    .ThenInclude(jel => jel.Account)
                .FirstOrDefaultAsync(je => je.Id == id);

            if (entry == null)
            {
                return NotFound();
            }

            return View(entry);
        }
    }
}