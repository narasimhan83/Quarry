using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuarryManagementSystem.Data;
using QuarryManagementSystem.Models.Domain;
using QuarryManagementSystem.ViewModels;

namespace QuarryManagementSystem.Controllers
{
    /// <summary>
    /// Catalogue CRUD for the inputs to production. Kept intentionally small
    /// in Phase 2 — name, unit, status. Pricing/procurement metadata can be
    /// added later if/when raw rock starts being purchased rather than
    /// self-extracted.
    /// </summary>
    [Authorize(Roles = "Admin,Manager")]
    public class RawMaterialController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<RawMaterialController> _logger;

        public RawMaterialController(ApplicationDbContext context, ILogger<RawMaterialController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: RawMaterial
        public async Task<IActionResult> Index()
        {
            var items = await _context.RawMaterials
                .OrderBy(r => r.Name)
                .ToListAsync();
            return View(items);
        }

        // GET: RawMaterial/Create
        public IActionResult Create()
        {
            return View(new RawMaterialEditViewModel());
        }

        // POST: RawMaterial/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(RawMaterialEditViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            // Soft duplicate check on name. Not enforced at the DB level because
            // operators occasionally rename or carry forward old entries that
            // happen to collide; we just warn and let them retry.
            var clash = await _context.RawMaterials
                .AnyAsync(r => r.Name == model.Name);
            if (clash)
            {
                ModelState.AddModelError(nameof(model.Name), "A raw material with this name already exists.");
                return View(model);
            }

            var entity = new RawMaterial
            {
                Name = model.Name.Trim(),
                Unit = string.IsNullOrWhiteSpace(model.Unit) ? "Ton" : model.Unit.Trim(),
                Status = model.Status,
                CreatedAt = DateTime.Now
            };
            _context.RawMaterials.Add(entity);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Raw material '{entity.Name}' added.";
            return RedirectToAction(nameof(Index));
        }

        // GET: RawMaterial/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var entity = await _context.RawMaterials.FindAsync(id.Value);
            if (entity == null) return NotFound();

            return View(new RawMaterialEditViewModel
            {
                Id = entity.Id,
                Name = entity.Name,
                Unit = entity.Unit,
                Status = entity.Status
            });
        }

        // POST: RawMaterial/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, RawMaterialEditViewModel model)
        {
            if (id != model.Id) return NotFound();
            if (!ModelState.IsValid) return View(model);

            var entity = await _context.RawMaterials.FindAsync(id);
            if (entity == null) return NotFound();

            // Same soft duplicate check, excluding self.
            var clash = await _context.RawMaterials
                .AnyAsync(r => r.Id != id && r.Name == model.Name);
            if (clash)
            {
                ModelState.AddModelError(nameof(model.Name), "Another raw material with this name already exists.");
                return View(model);
            }

            entity.Name = model.Name.Trim();
            entity.Unit = string.IsNullOrWhiteSpace(model.Unit) ? "Ton" : model.Unit.Trim();
            entity.Status = model.Status;

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Raw material '{entity.Name}' updated.";
            return RedirectToAction(nameof(Index));
        }

        // GET: RawMaterial/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var entity = await _context.RawMaterials
                .Include(r => r.Receipts)
                    .ThenInclude(rc => rc.Quarry)
                .FirstOrDefaultAsync(r => r.Id == id.Value);
            if (entity == null) return NotFound();
            return View(entity);
        }
    }
}
