using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuarryManagementSystem.Data;
using QuarryManagementSystem.ViewModels;

namespace QuarryManagementSystem.Controllers
{
    /// <summary>
    /// Read-only stock-on-hand viewer. Reads from MaterialCostState — the
    /// running cache maintained by IInventoryService — so this stays fast
    /// even with a large StockMovements log.
    /// <para/>
    /// Phase 6 will add proper movement history and valuation reports; for
    /// now this is the bare-minimum view of "what do we have, where, at what
    /// cost?"
    /// </summary>
    [Authorize(Roles = "Admin,Manager,Accountant")]
    public class StockController : Controller
    {
        private readonly ApplicationDbContext _context;

        public StockController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Stock
        public async Task<IActionResult> Index(int? quarryId, string? kind)
        {
            var query = _context.MaterialCostStates
                .Include(s => s.Quarry)
                .Include(s => s.Material)
                .Include(s => s.RawMaterial)
                .AsQueryable();

            if (quarryId.HasValue) query = query.Where(s => s.QuarryId == quarryId.Value);

            // "kind" filter: a state row is either Finished (MaterialId set) or
            // Raw (RawMaterialId set) — never both. We branch on the EF query
            // so we don't pull rows we'll discard.
            if (string.Equals(kind, "Finished", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(s => s.MaterialId != null);
            }
            else if (string.Equals(kind, "Raw", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(s => s.RawMaterialId != null);
            }

            // Drop empty / archival rows — quantity at zero AND no cost left
            // means there's nothing useful to show. We keep zero-qty rows that
            // still carry residual cost (rounding artefacts) for transparency.
            query = query.Where(s => s.QuantityOnHand > 0 || s.TotalCostOnHand != 0);

            var states = await query.ToListAsync();

            var rows = states
                .Select(s => new StockOnHandRow
                {
                    QuarryId = s.QuarryId,
                    QuarryName = s.Quarry?.Name ?? "(unknown)",
                    ItemName = s.MaterialId.HasValue
                        ? (s.Material?.Name ?? "(unknown material)")
                        : (s.RawMaterial?.Name ?? "(unknown raw material)"),
                    Kind = s.MaterialId.HasValue ? "Finished" : "Raw",
                    QuantityOnHand = s.QuantityOnHand,
                    TotalCostOnHand = s.TotalCostOnHand,
                    LastUpdated = s.LastUpdated
                })
                .OrderBy(r => r.QuarryName)
                .ThenBy(r => r.Kind)
                .ThenBy(r => r.ItemName)
                .ToList();

            ViewBag.Quarries = await _context.Quarries
                .OrderBy(q => q.Name)
                .Select(q => new SelectListItem { Value = q.Id.ToString(), Text = q.Name })
                .ToListAsync();

            return View(new StockOnHandViewModel
            {
                Rows = rows,
                QuarryFilter = quarryId,
                KindFilter = kind
            });
        }
    }
}
