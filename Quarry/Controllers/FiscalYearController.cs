using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuarryManagementSystem.Data;
using QuarryManagementSystem.Models.Domain;

namespace QuarryManagementSystem.Controllers
{
    [Authorize(Roles = "Admin,Manager,Accountant")]
    public class FiscalYearController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<FiscalYearController> _logger;

        public FiscalYearController(ApplicationDbContext context, ILogger<FiscalYearController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: FiscalYear
        public async Task<IActionResult> Index()
        {
            var years = await _context.FiscalYears
                .OrderByDescending(fy => fy.StartDate)
                .ToListAsync();

            return View(years);
        }

        // GET: FiscalYear/Create
        [HttpGet]
        public IActionResult Create()
        {
            var model = new FiscalYear
            {
                StartDate = new DateTime(DateTime.Today.Year, 1, 1),
                EndDate = new DateTime(DateTime.Today.Year, 12, 31)
            };

            return View(model);
        }

        // POST: FiscalYear/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(FiscalYear model)
        {
            try
            {
                if (model.EndDate < model.StartDate)
                {
                    ModelState.AddModelError(nameof(FiscalYear.EndDate), "End Date must be on or after Start Date.");
                }

                if (!await IsDateRangeAvailable(model.StartDate, model.EndDate, null))
                {
                    ModelState.AddModelError(string.Empty, "The selected date range overlaps with an existing fiscal year.");
                }

                if (ModelState.IsValid)
                {
                    model.CreatedAt = DateTime.Now;

                    // If this is the first fiscal year, mark it as current
                    if (!await _context.FiscalYears.AnyAsync())
                    {
                        model.IsCurrent = true;
                    }

                    _context.FiscalYears.Add(model);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = "Fiscal year created successfully.";
                    return RedirectToAction(nameof(Index));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating fiscal year");
                ModelState.AddModelError(string.Empty, "An error occurred while creating the fiscal year.");
            }

            return View(model);
        }

        // GET: FiscalYear/Edit/5
        [HttpGet]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var fiscalYear = await _context.FiscalYears.FindAsync(id.Value);
            if (fiscalYear == null)
            {
                return NotFound();
            }

            return View(fiscalYear);
        }

        // POST: FiscalYear/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, FiscalYear model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            var fiscalYear = await _context.FiscalYears.FindAsync(id);
            if (fiscalYear == null)
            {
                return NotFound();
            }

            if (fiscalYear.IsClosed)
            {
                ModelState.AddModelError(string.Empty, "A closed fiscal year cannot be edited.");
                return View(fiscalYear);
            }

            try
            {
                if (model.EndDate < model.StartDate)
                {
                    ModelState.AddModelError(nameof(FiscalYear.EndDate), "End Date must be on or after Start Date.");
                }

                if (!await IsDateRangeAvailable(model.StartDate, model.EndDate, model.Id))
                {
                    ModelState.AddModelError(string.Empty, "The selected date range overlaps with an existing fiscal year.");
                }

                if (ModelState.IsValid)
                {
                    fiscalYear.YearCode = model.YearCode;
                    fiscalYear.StartDate = model.StartDate;
                    fiscalYear.EndDate = model.EndDate;

                    await _context.SaveChangesAsync();

                    TempData["Success"] = "Fiscal year updated successfully.";
                    return RedirectToAction(nameof(Index));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editing fiscal year {FiscalYearId}", id);
                ModelState.AddModelError(string.Empty, "An error occurred while updating the fiscal year.");
            }

            return View(model);
        }

        // POST: FiscalYear/SetCurrent/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetCurrent(int id)
        {
            try
            {
                var fiscalYear = await _context.FiscalYears.FindAsync(id);
                if (fiscalYear == null)
                {
                    return NotFound();
                }

                if (fiscalYear.IsClosed)
                {
                    TempData["Error"] = "A closed fiscal year cannot be set as current.";
                    return RedirectToAction(nameof(Index));
                }

                var allYears = await _context.FiscalYears.ToListAsync();
                foreach (var fy in allYears)
                {
                    fy.IsCurrent = fy.Id == id;
                }

                await _context.SaveChangesAsync();
                TempData["Success"] = $"Fiscal year {fiscalYear.YearCode} set as current.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting current fiscal year {FiscalYearId}", id);
                TempData["Error"] = "An error occurred while setting the current fiscal year.";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: FiscalYear/Close/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Close(int id)
        {
            try
            {
                var fiscalYear = await _context.FiscalYears.FindAsync(id);
                if (fiscalYear == null)
                {
                    return NotFound();
                }

                if (fiscalYear.IsClosed)
                {
                    TempData["Error"] = "This fiscal year is already closed.";
                    return RedirectToAction(nameof(Index));
                }

                fiscalYear.IsClosed = true;

                // When closing a fiscal year, it should no longer be considered current
                if (fiscalYear.IsCurrent)
                {
                    fiscalYear.IsCurrent = false;
                }

                await _context.SaveChangesAsync();
                TempData["Success"] = $"Fiscal year {fiscalYear.YearCode} closed successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error closing fiscal year {FiscalYearId}", id);
                TempData["Error"] = "An error occurred while closing the fiscal year.";
            }

            return RedirectToAction(nameof(Index));
        }

        private async Task<bool> IsDateRangeAvailable(DateTime start, DateTime end, int? excludeId)
        {
            return !await _context.FiscalYears
                .Where(fy => !excludeId.HasValue || fy.Id != excludeId.Value)
                .AnyAsync(fy =>
                    start <= fy.EndDate &&
                    end >= fy.StartDate);
        }
    }
}