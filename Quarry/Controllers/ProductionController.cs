using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuarryManagementSystem.Data;
using QuarryManagementSystem.Models.Domain;
using QuarryManagementSystem.Services;
using QuarryManagementSystem.ViewModels;

namespace QuarryManagementSystem.Controllers
{
    /// <summary>
    /// Production runs: one input (raw material) → many outputs (finished
    /// materials) at a single quarry. Lifecycle is Draft → Posted → Cancelled.
    /// <para/>
    /// Phase 2 scope:
    ///   - Draft: free editing, no inventory impact.
    ///   - Post: validates mass balance, draws raw inventory at WAC, writes
    ///     stock movements, allocates cost across outputs by weight share,
    ///     updates finished-goods cost states. No journal entries yet.
    ///   - Cancel (on Posted): reverses all of the above via the inventory
    ///     service. Once cancelled, the run is read-only.
    /// </summary>
    [Authorize(Roles = "Admin,Manager")]
    public class ProductionController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IInventoryService _inventory;
        private readonly ILogger<ProductionController> _logger;

        public ProductionController(
            ApplicationDbContext context,
            IInventoryService inventory,
            ILogger<ProductionController> logger)
        {
            _context = context;
            _inventory = inventory;
            _logger = logger;
        }

        // GET: Production
        public async Task<IActionResult> Index(int? quarryId, string? status, DateTime? fromDate, DateTime? toDate)
        {
            var query = _context.ProductionRuns
                .Include(p => p.Quarry)
                .Include(p => p.RawMaterial)
                .Include(p => p.Outputs)
                    .ThenInclude(o => o.Material)
                .AsQueryable();

            if (quarryId.HasValue) query = query.Where(p => p.QuarryId == quarryId.Value);
            if (!string.IsNullOrWhiteSpace(status)) query = query.Where(p => p.Status == status);
            if (fromDate.HasValue) query = query.Where(p => p.RunDate >= fromDate.Value);
            if (toDate.HasValue) query = query.Where(p => p.RunDate <= toDate.Value);

            var items = await query
                .OrderByDescending(p => p.RunDate)
                .ThenByDescending(p => p.Id)
                .ToListAsync();

            ViewBag.Quarries = await GetQuarryDropdownAsync();
            return View(items);
        }

        // GET: Production/Create
        public async Task<IActionResult> Create()
        {
            var model = new ProductionRunEditViewModel
            {
                RunDate = DateTime.Today,
                Status = "Draft"
            };
            await PopulateDropdownsAsync(model);
            return View(model);
        }

        // POST: Production/Create
        // A new run always lands in Draft. Post happens via the Post action.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProductionRunEditViewModel model)
        {
            // Strip empty rows that operators left as-is — only keep lines
            // with both a material and a positive quantity.
            var validOutputs = (model.Outputs ?? new List<ProductionOutputInput>())
                .Where(o => o.MaterialId.HasValue && o.MaterialId > 0 && o.Quantity > 0)
                .ToList();
            if (!validOutputs.Any())
            {
                ModelState.AddModelError(string.Empty, "Add at least one output line with a material and a positive quantity.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateDropdownsAsync(model);
                return View(model);
            }

            var runNumber = string.IsNullOrWhiteSpace(model.RunNumber)
                ? await GenerateRunNumberAsync()
                : model.RunNumber.Trim();

            var clash = await _context.ProductionRuns.AnyAsync(p => p.RunNumber == runNumber);
            if (clash)
            {
                ModelState.AddModelError(nameof(model.RunNumber), "Run number already exists. Leave blank to auto-generate.");
                await PopulateDropdownsAsync(model);
                return View(model);
            }

            var run = new ProductionRun
            {
                RunNumber = runNumber,
                QuarryId = model.QuarryId!.Value,
                RunDate = model.RunDate,
                RawMaterialId = model.RawMaterialId!.Value,
                InputQuantity = model.InputQuantity,
                InputTotalCost = 0m,        // set at Post time
                WasteQuantity = model.WasteQuantity,
                Status = "Draft",
                Operator = model.Operator,
                Notes = model.Notes,
                CreatedBy = User.Identity?.Name,
                CreatedAt = DateTime.Now
            };
            foreach (var o in validOutputs)
            {
                run.Outputs.Add(new ProductionRunOutput
                {
                    MaterialId = o.MaterialId!.Value,
                    Quantity = o.Quantity,
                    AllocatedCost = 0m      // set at Post time
                });
            }

            _context.ProductionRuns.Add(run);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Production run {run.RunNumber} created in Draft. Review and Post when ready.";
            return RedirectToAction(nameof(Details), new { id = run.Id });
        }

        // GET: Production/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var run = await _context.ProductionRuns
                .Include(p => p.Outputs)
                .FirstOrDefaultAsync(p => p.Id == id.Value);
            if (run == null) return NotFound();

            // Posted / Cancelled runs are read-only. Use Cancel to undo a Posted run.
            if (!string.Equals(run.Status, "Draft", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = $"This run is {run.Status} and cannot be edited. View its details or cancel it first.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var model = new ProductionRunEditViewModel
            {
                Id = run.Id,
                RunNumber = run.RunNumber,
                QuarryId = run.QuarryId,
                RunDate = run.RunDate,
                RawMaterialId = run.RawMaterialId,
                InputQuantity = run.InputQuantity,
                WasteQuantity = run.WasteQuantity,
                Operator = run.Operator,
                Notes = run.Notes,
                Status = run.Status,
                Outputs = run.Outputs
                    .OrderBy(o => o.Id)
                    .Select(o => new ProductionOutputInput
                    {
                        Id = o.Id,
                        MaterialId = o.MaterialId,
                        Quantity = o.Quantity,
                        AllocatedCost = o.AllocatedCost
                    })
                    .ToList()
            };
            await PopulateDropdownsAsync(model);
            return View(model);
        }

        // POST: Production/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ProductionRunEditViewModel model)
        {
            if (id != model.Id) return NotFound();

            var run = await _context.ProductionRuns
                .Include(p => p.Outputs)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (run == null) return NotFound();

            if (!string.Equals(run.Status, "Draft", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = $"This run is {run.Status} and cannot be edited.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var validOutputs = (model.Outputs ?? new List<ProductionOutputInput>())
                .Where(o => o.MaterialId.HasValue && o.MaterialId > 0 && o.Quantity > 0)
                .ToList();
            if (!validOutputs.Any())
            {
                ModelState.AddModelError(string.Empty, "Add at least one output line with a material and a positive quantity.");
            }
            if (!ModelState.IsValid)
            {
                await PopulateDropdownsAsync(model);
                return View(model);
            }

            // Update header.
            run.QuarryId = model.QuarryId!.Value;
            run.RunDate = model.RunDate;
            run.RawMaterialId = model.RawMaterialId!.Value;
            run.InputQuantity = model.InputQuantity;
            run.WasteQuantity = model.WasteQuantity;
            run.Operator = model.Operator;
            run.Notes = model.Notes;

            // Replace outputs wholesale. Safe in Draft because nothing has been
            // posted yet; no movements / state to reverse.
            _context.ProductionRunOutputs.RemoveRange(run.Outputs);
            run.Outputs.Clear();
            foreach (var o in validOutputs)
            {
                run.Outputs.Add(new ProductionRunOutput
                {
                    MaterialId = o.MaterialId!.Value,
                    Quantity = o.Quantity,
                    AllocatedCost = 0m
                });
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Run {run.RunNumber} updated.";
            return RedirectToAction(nameof(Details), new { id = run.Id });
        }

        // GET: Production/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var run = await _context.ProductionRuns
                .Include(p => p.Quarry)
                .Include(p => p.RawMaterial)
                .Include(p => p.Outputs)
                    .ThenInclude(o => o.Material)
                .FirstOrDefaultAsync(p => p.Id == id.Value);
            if (run == null) return NotFound();

            return View(new ProductionRunDetailsViewModel { Run = run });
        }

        // POST: Production/Post/5
        // Transitions a Draft run to Posted: pulls raw inventory at WAC,
        // allocates cost across outputs, writes stock movements. Must be
        // wrapped in a single DB transaction so a partial failure rolls back.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Post(int id)
        {
            var run = await _context.ProductionRuns
                .Include(p => p.Outputs)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (run == null) return NotFound();

            if (!string.Equals(run.Status, "Draft", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = $"Only Draft runs can be Posted. This one is {run.Status}.";
                return RedirectToAction(nameof(Details), new { id });
            }

            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                await _inventory.PostProductionRunAsync(run, User.Identity?.Name);

                run.Status = "Posted";
                run.PostedAt = DateTime.Now;
                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                TempData["Success"] = $"Run {run.RunNumber} posted. " +
                    $"Input cost \u20A6{run.InputTotalCost:N2} allocated across " +
                    $"{run.Outputs.Count} output line(s).";
                _logger.LogInformation(
                    "Production run {RunNumber} posted by {User}. Input cost {Cost}.",
                    run.RunNumber, User.Identity?.Name, run.InputTotalCost);
            }
            catch (InvalidOperationException ex)
            {
                // Expected business-rule failures (insufficient stock, mass
                // balance off, etc.) — surface the message to the operator.
                await tx.RollbackAsync();
                TempData["Error"] = ex.Message;
                _logger.LogWarning(ex, "Production run {RunNumber} could not be posted.", run.RunNumber);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "Unexpected error posting production run {RunNumber}.", run.RunNumber);
                TempData["Error"] = "Could not post the run. Please try again or contact support if it persists.";
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Production/Cancel/5
        // Cancels a Posted run by reversing every movement and state change.
        // Draft runs can be cancelled too — that's just a soft delete (no
        // inventory impact to reverse).
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var run = await _context.ProductionRuns
                .Include(p => p.Outputs)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (run == null) return NotFound();

            if (string.Equals(run.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Run is already Cancelled.";
                return RedirectToAction(nameof(Details), new { id });
            }

            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                if (string.Equals(run.Status, "Posted", StringComparison.OrdinalIgnoreCase))
                {
                    await _inventory.ReverseProductionRunAsync(run, User.Identity?.Name);
                }

                run.Status = "Cancelled";
                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                TempData["Success"] = $"Run {run.RunNumber} cancelled.";
                _logger.LogInformation(
                    "Production run {RunNumber} cancelled by {User}.",
                    run.RunNumber, User.Identity?.Name);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "Error cancelling production run {RunNumber}.", run.RunNumber);
                TempData["Error"] = "Could not cancel the run. Please try again.";
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        // ---------- Helpers ----------

        private async Task PopulateDropdownsAsync(ProductionRunEditViewModel model)
        {
            model.Quarries = await GetQuarryDropdownAsync();
            model.RawMaterials = await _context.RawMaterials
                .Where(r => r.Status == "Active")
                .OrderBy(r => r.Name)
                .Select(r => new SelectListItem { Value = r.Id.ToString(), Text = r.Name })
                .ToListAsync();
            model.Materials = await _context.Materials
                .Where(m => m.Status == "Active")
                .OrderBy(m => m.Name)
                .Select(m => new SelectListItem { Value = m.Id.ToString(), Text = $"{m.Name} ({m.Type})" })
                .ToListAsync();
        }

        private async Task<List<SelectListItem>> GetQuarryDropdownAsync()
        {
            return await _context.Quarries
                .Where(q => q.Status == "Active")
                .OrderBy(q => q.Name)
                .Select(q => new SelectListItem { Value = q.Id.ToString(), Text = q.Name })
                .ToListAsync();
        }

        /// <summary>
        /// Generates the next run number in the format PR-YYYYMM-NNNN. Scoped
        /// per-month to keep numbers short.
        /// </summary>
        private async Task<string> GenerateRunNumberAsync()
        {
            var prefix = $"PR-{DateTime.Today:yyyyMM}-";
            var existing = await _context.ProductionRuns
                .Where(p => p.RunNumber.StartsWith(prefix))
                .Select(p => p.RunNumber)
                .ToListAsync();

            int max = 0;
            foreach (var n in existing)
            {
                var tail = n.Substring(prefix.Length);
                if (int.TryParse(tail, out var v) && v > max) max = v;
            }
            return $"{prefix}{(max + 1):D4}";
        }
    }
}
