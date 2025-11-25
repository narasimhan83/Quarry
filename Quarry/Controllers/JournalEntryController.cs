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
        
                    DateTime from;
                    DateTime to;
        
                    if (fromDate.HasValue || toDate.HasValue)
                    {
                        // Use provided filters (if only one bound is provided, make the other very wide)
                        from = fromDate ?? DateTime.MinValue;
                        to = toDate ?? DateTime.MaxValue;
        
                        query = query.Where(je => je.EntryDate.Date >= from.Date && je.EntryDate.Date <= to.Date);
                    }
                    else
                    {
                        // No filters provided: show all journal entries
                        from = DateTime.MinValue;
                        to = DateTime.MaxValue;
                    }
        
                    // Only set filter boxes when there is an explicit value; otherwise leave empty so the user
                    // can choose their own range without being confused by hidden defaults.
                    ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd") ?? string.Empty;
                    ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd") ?? string.Empty;
        
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