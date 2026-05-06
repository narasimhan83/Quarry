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
    /// Records raw material arriving at a quarry yard. Each Create writes
    /// (a) a RawMaterialReceipt row, (b) a +qty StockMovement, and
    /// (c) updates the running MaterialCostState — all in a single
    /// transaction via IInventoryService.
    /// <para/>
    /// Edit is intentionally NOT supported in Phase 2. Once a receipt is
    /// saved it has cascaded into a stock movement and a WAC change; allowing
    /// arbitrary edits would require reversing those, which is the kind of
    /// thing better handled by a "Cancel + re-create" flow than by overlapping
    /// edits. Phase 7 (year-end / adjustments) revisits this if needed.
    /// </summary>
    [Authorize(Roles = "Admin,Manager,Accountant")]
    public class RawMaterialReceiptController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IInventoryService _inventory;
        private readonly ILogger<RawMaterialReceiptController> _logger;

        public RawMaterialReceiptController(
            ApplicationDbContext context,
            IInventoryService inventory,
            ILogger<RawMaterialReceiptController> logger)
        {
            _context = context;
            _inventory = inventory;
            _logger = logger;
        }

        // GET: RawMaterialReceipt
        public async Task<IActionResult> Index(int? quarryId, int? rawMaterialId, DateTime? fromDate, DateTime? toDate)
        {
            var query = _context.RawMaterialReceipts
                .Include(r => r.Quarry)
                .Include(r => r.RawMaterial)
                .AsQueryable();

            if (quarryId.HasValue)
                query = query.Where(r => r.QuarryId == quarryId.Value);
            if (rawMaterialId.HasValue)
                query = query.Where(r => r.RawMaterialId == rawMaterialId.Value);
            if (fromDate.HasValue)
                query = query.Where(r => r.ReceiptDate >= fromDate.Value);
            if (toDate.HasValue)
                query = query.Where(r => r.ReceiptDate <= toDate.Value);

            var items = await query
                .OrderByDescending(r => r.ReceiptDate)
                .ThenByDescending(r => r.Id)
                .ToListAsync();

            ViewBag.Quarries = await GetQuarryDropdownAsync();
            ViewBag.RawMaterials = await GetRawMaterialDropdownAsync();
            return View(items);
        }

        // GET: RawMaterialReceipt/Create
        public async Task<IActionResult> Create()
        {
            var model = new RawMaterialReceiptEditViewModel
            {
                ReceiptDate = DateTime.Today
            };
            await PopulateDropdownsAsync(model);
            return View(model);
        }

        // POST: RawMaterialReceipt/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(RawMaterialReceiptEditViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await PopulateDropdownsAsync(model);
                return View(model);
            }

            // Generate receipt number if blank: RR-YYYYMM-NNNN.
            var receiptNumber = string.IsNullOrWhiteSpace(model.ReceiptNumber)
                ? await GenerateReceiptNumberAsync()
                : model.ReceiptNumber.Trim();

            // Reject duplicates explicitly (DB has a unique index, but a friendly
            // error is better than a 500 page).
            var clash = await _context.RawMaterialReceipts
                .AnyAsync(r => r.ReceiptNumber == receiptNumber);
            if (clash)
            {
                ModelState.AddModelError(nameof(model.ReceiptNumber), "Receipt number already exists. Leave blank to auto-generate.");
                await PopulateDropdownsAsync(model);
                return View(model);
            }

            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var receipt = new RawMaterialReceipt
                {
                    ReceiptNumber = receiptNumber,
                    QuarryId = model.QuarryId!.Value,
                    RawMaterialId = model.RawMaterialId!.Value,
                    ReceiptDate = model.ReceiptDate,
                    Quantity = model.Quantity,
                    UnitCost = model.UnitCost,
                    Source = model.Source,
                    Notes = model.Notes,
                    CreatedBy = User.Identity?.Name,
                    CreatedAt = DateTime.Now
                };

                // Persist the receipt first so it gets an Id, then have the
                // inventory service write the linked StockMovement + state.
                _context.RawMaterialReceipts.Add(receipt);
                await _context.SaveChangesAsync();

                await _inventory.RecordRawReceiptAsync(receipt, User.Identity?.Name);
                await _context.SaveChangesAsync();

                await tx.CommitAsync();

                TempData["Success"] = $"Receipt {receipt.ReceiptNumber} recorded: " +
                    $"{receipt.Quantity:N3} of raw material added to inventory.";
                _logger.LogInformation(
                    "Raw material receipt {Number} recorded by {User}: {Qty} units at {Cost} each",
                    receipt.ReceiptNumber, User.Identity?.Name, receipt.Quantity, receipt.UnitCost);
                return RedirectToAction(nameof(Details), new { id = receipt.Id });
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "Error saving raw material receipt; rolled back.");
                ModelState.AddModelError(string.Empty, "Could not save the receipt. Please try again.");
                await PopulateDropdownsAsync(model);
                return View(model);
            }
        }

        // GET: RawMaterialReceipt/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var receipt = await _context.RawMaterialReceipts
                .Include(r => r.Quarry)
                .Include(r => r.RawMaterial)
                .FirstOrDefaultAsync(r => r.Id == id.Value);
            if (receipt == null) return NotFound();
            return View(receipt);
        }

        // ---------- Helpers ----------

        private async Task PopulateDropdownsAsync(RawMaterialReceiptEditViewModel model)
        {
            model.Quarries = await GetQuarryDropdownAsync();
            model.RawMaterials = await GetRawMaterialDropdownAsync();
        }

        private async Task<List<SelectListItem>> GetQuarryDropdownAsync()
        {
            return await _context.Quarries
                .Where(q => q.Status == "Active")
                .OrderBy(q => q.Name)
                .Select(q => new SelectListItem { Value = q.Id.ToString(), Text = q.Name })
                .ToListAsync();
        }

        private async Task<List<SelectListItem>> GetRawMaterialDropdownAsync()
        {
            return await _context.RawMaterials
                .Where(r => r.Status == "Active")
                .OrderBy(r => r.Name)
                .Select(r => new SelectListItem { Value = r.Id.ToString(), Text = r.Name })
                .ToListAsync();
        }

        /// <summary>
        /// Generates the next receipt number in the format RR-YYYYMM-NNNN.
        /// Scoped per-month so the running counter resets each month, which
        /// keeps the numbers short and readable.
        /// </summary>
        private async Task<string> GenerateReceiptNumberAsync()
        {
            var prefix = $"RR-{DateTime.Today:yyyyMM}-";
            var existing = await _context.RawMaterialReceipts
                .Where(r => r.ReceiptNumber.StartsWith(prefix))
                .Select(r => r.ReceiptNumber)
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
