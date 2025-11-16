using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuarryManagementSystem.Data;
using QuarryManagementSystem.Models.Domain;
using QuarryManagementSystem.ViewModels;

namespace QuarryManagementSystem.Controllers
{
    [Authorize(Roles = "Admin,Manager,Accountant")]
    public class MaterialController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<MaterialController> _logger;

        public MaterialController(ApplicationDbContext context, ILogger<MaterialController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: Material
        public async Task<IActionResult> Index(string searchTerm, string type, string status, int page = 1)
        {
            try
            {
                int pageSize = 20;
                var query = _context.Materials
                    .Include(m => m.StockYards)
                    .AsQueryable();

                // Apply filters
                if (!string.IsNullOrEmpty(searchTerm))
                {
                    query = query.Where(m => 
                        m.Name.Contains(searchTerm) || 
                        m.Type.Contains(searchTerm));
                }

                if (!string.IsNullOrEmpty(type))
                {
                    query = query.Where(m => m.Type == type);
                }

                if (!string.IsNullOrEmpty(status))
                {
                    query = query.Where(m => m.Status == status);
                }

                // Get total count for pagination
                var totalCount = await query.CountAsync();
                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

                // Apply pagination
                var materials = await query
                    .OrderBy(m => m.Name)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var viewModel = new MaterialListViewModel
                {
                    Materials = materials,
                    SearchTerm = searchTerm,
                    SelectedType = type,
                    SelectedStatus = status,
                    CurrentPage = page,
                    TotalPages = totalPages,
                    TotalCount = totalCount,
                    Types = GetMaterialTypes(),
                    Statuses = GetMaterialStatuses()
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading material list");
                return View(new MaterialListViewModel
                {
                    ErrorMessage = "An error occurred while loading materials. Please try again."
                });
            }
        }

        // GET: Material/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var material = await _context.Materials
                .Include(m => m.StockYards)
                .ThenInclude(sy => sy.Quarry)
                .Include(m => m.WeighmentTransactions)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (material == null)
            {
                return NotFound();
            }

            return View(material);
        }

        // GET: Material/Create
        public IActionResult Create()
        {
            ViewBag.Types = GetMaterialTypes();
            ViewBag.Statuses = GetMaterialStatuses();
            return View();
        }

        // POST: Material/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Material material)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    // Check if material name already exists
                    if (await _context.Materials.AnyAsync(m => m.Name == material.Name))
                    {
                        ModelState.AddModelError("Name", "A material with this name already exists.");
                    }
                    else
                    {
                        material.CreatedAt = DateTime.Now;
                        material.Status = "Active";

                        _context.Add(material);
                        await _context.SaveChangesAsync();

                        // Create initial stock yard entry for default quarry
                        var defaultQuarry = await _context.Quarries.FirstOrDefaultAsync();
                        if (defaultQuarry != null)
                        {
                            var stockYard = new StockYard
                            {
                                QuarryId = defaultQuarry.Id,
                                MaterialId = material.Id,
                                CurrentStock = 0,
                                ReservedStock = 0,
                                LastUpdated = DateTime.Now
                            };
                            _context.StockYards.Add(stockYard);
                            await _context.SaveChangesAsync();
                        }

                        TempData["Success"] = "Material created successfully.";
                        _logger.LogInformation("Material {MaterialName} created by user {UserName}", 
                            material.Name, User.Identity?.Name);
                        
                        return RedirectToAction(nameof(Index));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating material");
                ModelState.AddModelError("", "An error occurred while creating the material. Please try again.");
            }

            ViewBag.Types = GetMaterialTypes();
            ViewBag.Statuses = GetMaterialStatuses();
            return View(material);
        }

        // GET: Material/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var material = await _context.Materials.FindAsync(id);
            if (material == null)
            {
                return NotFound();
            }

            ViewBag.Types = GetMaterialTypes();
            ViewBag.Statuses = GetMaterialStatuses();
            return View(material);
        }

        // POST: Material/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Material material)
        {
            if (id != material.Id)
            {
                return NotFound();
            }

            try
            {
                if (ModelState.IsValid)
                {
                    // Check if material name already exists for another material
                    if (await _context.Materials.AnyAsync(m => m.Name == material.Name && m.Id != material.Id))
                    {
                        ModelState.AddModelError("Name", "Another material with this name already exists.");
                    }
                    else
                    {
                        try
                        {
                            material.UpdatedAt = DateTime.Now;
                            _context.Update(material);
                            await _context.SaveChangesAsync();

                            TempData["Success"] = "Material updated successfully.";
                            _logger.LogInformation("Material {MaterialName} updated by user {UserName}", 
                                material.Name, User.Identity?.Name);
                        }
                        catch (DbUpdateConcurrencyException)
                        {
                            if (!MaterialExists(material.Id))
                            {
                                return NotFound();
                            }
                            else
                            {
                                throw;
                            }
                        }
                        return RedirectToAction(nameof(Index));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating material");
                ModelState.AddModelError("", "An error occurred while updating the material. Please try again.");
            }

            ViewBag.Types = GetMaterialTypes();
            ViewBag.Statuses = GetMaterialStatuses();
            return View(material);
        }

        // GET: Material/PriceHistory/5
        public async Task<IActionResult> PriceHistory(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var material = await _context.Materials.FindAsync(id);
            if (material == null)
            {
                return NotFound();
            }

            // In a real implementation, you would have a price history table
            // For now, we'll just show the current material
            return View(material);
        }

        // GET: Material/StockAdjustment/5
        public async Task<IActionResult> StockAdjustment(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var material = await _context.Materials
                .Include(m => m.StockYards)
                .ThenInclude(sy => sy.Quarry)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (material == null)
            {
                return NotFound();
            }

            var viewModel = new MaterialStockAdjustmentViewModel
            {
                MaterialId = material.Id,
                MaterialName = material.Name,
                CurrentStock = material.StockYards.Sum(sy => sy.CurrentStock),
                StockYards = material.StockYards.ToList()
            };

            return View(viewModel);
        }

        // POST: Material/StockAdjustment/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StockAdjustment(MaterialStockAdjustmentViewModel model)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    var material = await _context.Materials
                        .Include(m => m.StockYards)
                        .FirstOrDefaultAsync(m => m.Id == model.MaterialId);

                    if (material == null)
                    {
                        return NotFound();
                    }

                    // Apply stock adjustment
                    foreach (var stockYard in material.StockYards)
                    {
                        if (model.Adjustments.ContainsKey(stockYard.Id))
                        {
                            var adjustment = model.Adjustments[stockYard.Id];
                            stockYard.CurrentStock += adjustment;
                            stockYard.LastUpdated = DateTime.Now;

                            // Log the adjustment
                            _logger.LogInformation("Stock adjustment for material {MaterialName} at quarry {QuarryId}: {Adjustment} tons by user {UserName}", 
                                material.Name, stockYard.QuarryId, adjustment, User.Identity?.Name);
                        }
                    }

                    await _context.SaveChangesAsync();

                    TempData["Success"] = "Stock adjustment completed successfully.";
                    return RedirectToAction(nameof(Details), new { id = model.MaterialId });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adjusting stock for material {MaterialId}", model.MaterialId);
                ModelState.AddModelError("", "An error occurred while adjusting stock. Please try again.");
            }

            // Reload data for view
            var materialReload = await _context.Materials
                .Include(m => m.StockYards)
                .ThenInclude(sy => sy.Quarry)
                .FirstOrDefaultAsync(m => m.Id == model.MaterialId);

            if (materialReload != null)
            {
                model.MaterialName = materialReload.Name;
                model.CurrentStock = materialReload.StockYards.Sum(sy => sy.CurrentStock);
                model.StockYards = materialReload.StockYards.ToList();
            }

            return View(model);
        }

        // AJAX: Get material stock levels
        [HttpGet]
        public async Task<JsonResult> GetStockLevels(int materialId)
        {
            try
            {
                var stockYards = await _context.StockYards
                    .Include(sy => sy.Quarry)
                    .Where(sy => sy.MaterialId == materialId)
                    .Select(sy => new
                    {
                        quarryName = sy.Quarry.Name,
                        currentStock = sy.CurrentStock,
                        reservedStock = sy.ReservedStock,
                        availableStock = sy.CurrentStock - sy.ReservedStock
                    })
                    .ToListAsync();

                return Json(new { success = true, data = stockYards });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting stock levels for material {MaterialId}", materialId);
                return Json(new { success = false, message = "Error retrieving stock levels" });
            }
        }

        // AJAX: Update material price
        [HttpPost]
        public async Task<JsonResult> UpdatePrice(int materialId, decimal newPrice, string reason)
        {
            try
            {
                var material = await _context.Materials.FindAsync(materialId);
                if (material == null)
                {
                    return Json(new { success = false, message = "Material not found" });
                }

                var oldPrice = material.UnitPrice;
                material.UnitPrice = newPrice;
                material.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                // Log the price change
                _logger.LogInformation("Price updated for material {MaterialName}: {OldPrice} -> {NewPrice} by user {UserName}. Reason: {Reason}", 
                    material.Name, oldPrice, newPrice, User.Identity?.Name, reason);

                return Json(new { success = true, message = "Price updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating price for material {MaterialId}", materialId);
                return Json(new { success = false, message = "Error updating price" });
            }
        }

        // Helper methods
        private bool MaterialExists(int id)
        {
            return _context.Materials.Any(e => e.Id == id);
        }

        private List<SelectListItem> GetMaterialTypes()
        {
            return new List<SelectListItem>
            {
                new SelectListItem { Value = "", Text = "-- Select Type --" },
                new SelectListItem { Value = "Aggregate", Text = "Aggregate" },
                new SelectListItem { Value = "Sand", Text = "Sand" },
                new SelectListItem { Value = "Dust", Text = "Dust" },
                new SelectListItem { Value = "Laterite", Text = "Laterite" },
                new SelectListItem { Value = "Stone", Text = "Stone" },
                new SelectListItem { Value = "Gravel", Text = "Gravel" }
            };
        }

        private List<SelectListItem> GetMaterialStatuses()
        {
            return new List<SelectListItem>
            {
                new SelectListItem { Value = "", Text = "-- Select Status --" },
                new SelectListItem { Value = "Active", Text = "Active" },
                new SelectListItem { Value = "Inactive", Text = "Inactive" }
            };
        }
    }
}