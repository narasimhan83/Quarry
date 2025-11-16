using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuarryManagementSystem.Data;
using QuarryManagementSystem.Models.Domain;
using QuarryManagementSystem.ViewModels;
using System.Linq.Expressions;

namespace QuarryManagementSystem.Controllers
{
    [Authorize(Roles = "Admin,Manager,Accountant,Operator")]
    public class WeighmentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<WeighmentController> _logger;

        public WeighmentController(ApplicationDbContext context, ILogger<WeighmentController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: Weighment
        public async Task<IActionResult> Index(string searchTerm, string status, DateTime? dateFrom, DateTime? dateTo, int page = 1)
        {
            try
            {
                int pageSize = 20;
                var query = _context.WeighmentTransactions
                    .Include(w => w.Customer)
                    .Include(w => w.Material)
                    .Include(w => w.Weighbridge)
                    .AsQueryable();

                // Apply filters
                if (!string.IsNullOrEmpty(searchTerm))
                {
                    query = query.Where(w => 
                        w.TransactionNumber.Contains(searchTerm) || 
                        w.VehicleRegNumber.Contains(searchTerm) ||
                        (w.Customer != null && w.Customer.Name.Contains(searchTerm)));
                }

                if (!string.IsNullOrEmpty(status))
                {
                    query = query.Where(w => w.Status == status);
                }

                if (dateFrom.HasValue)
                {
                    query = query.Where(w => w.TransactionDate >= dateFrom.Value);
                }

                if (dateTo.HasValue)
                {
                    query = query.Where(w => w.TransactionDate <= dateTo.Value.AddDays(1));
                }

                // Get total count for pagination
                var totalCount = await query.CountAsync();
                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

                // Apply pagination
                var weighments = await query
                    .OrderByDescending(w => w.TransactionDate)
                    .ThenByDescending(w => w.Id)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var viewModel = new WeighmentListViewModel
                {
                    Weighments = weighments,
                    SearchTerm = searchTerm,
                    SelectedStatus = status,
                    DateFrom = dateFrom,
                    DateTo = dateTo,
                    CurrentPage = page,
                    TotalPages = totalPages,
                    TotalCount = totalCount,
                    Statuses = GetWeighmentStatuses()
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading weighment list");
                return View(new WeighmentListViewModel
                {
                    ErrorMessage = "An error occurred while loading weighments. Please try again."
                });
            }
        }

        // GET: Weighment/Create
        public async Task<IActionResult> Create()
        {
            try
            {
                var viewModel = new WeighmentCreateViewModel
                {
                    TransactionDate = DateTime.Now,
                    VatRate = 7.5m, // Nigerian VAT rate
                    WeightUnit = "kg",
                    Status = "InProgress"
                };

                await PopulateDropdowns(viewModel);
                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading create weighment form");
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Weighment/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(WeighmentCreateViewModel model)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    // Validate customer credit limit - warn but do not block creation
                    if (model.CustomerId.HasValue)
                    {
                        var customer = await _context.Customers.FindAsync(model.CustomerId.Value);
                        if (customer != null)
                        {
                            var estimatedAmount = CalculateEstimatedAmount(model);
                            if (customer.HasExceededCreditLimit(estimatedAmount))
                            {
                                TempData["Error"] = $"Warning: customer exceeded credit limit. Available credit: {customer.AvailableCredit:C}. Weighment will still be created.";
                            }
                        }
                    }

                    // Determine transaction number (allow manual entry)
                    string transactionNumber = model.TransactionNumber;
                    if (string.IsNullOrWhiteSpace(transactionNumber))
                    {
                        transactionNumber = await GenerateNewTransactionNumber();
                    }
                    else
                    {
                        var exists = await _context.WeighmentTransactions.AnyAsync(w => w.TransactionNumber == transactionNumber);
                        if (exists)
                        {
                            ModelState.AddModelError("TransactionNumber", "Transaction number already exists. Please enter a unique number or use the generator.");
                            await PopulateDropdowns(model);
                            return View(model);
                        }
                    }
                    
                    var weighment = new WeighmentTransaction
                    {
                        TransactionNumber = transactionNumber,
                        TransactionDate = model.TransactionDate,
                        VehicleRegNumber = model.VehicleRegNumber,
                        DriverName = model.DriverName,
                        DriverPhone = model.DriverPhone,
                        CustomerId = model.CustomerId,
                        WeighbridgeId = model.WeighbridgeId,
                        MaterialId = model.MaterialId,
                        PricePerUnit = model.PricePerUnit,
                        VatRate = model.VatRate,
                        GrossWeight = model.GrossWeight,
                        TareWeight = model.TareWeight,
                        WeightUnit = model.WeightUnit,
                        EntryTime = model.EntryTime,
                        ExitTime = model.ExitTime,
                        TransactionType = model.TransactionType,
                        Status = model.Status,
                        ChallanNumber = model.ChallanNumber,
                        CreatedBy = User.Identity?.Name,
                        CreatedAt = DateTime.Now
                    };

                    // Calculate financials
                    weighment.CalculateFinancials();

                    _context.Add(weighment);
                    await _context.SaveChangesAsync();

                    // Update customer outstanding balance if applicable
                    if (weighment.CustomerId.HasValue && weighment.TotalAmount.HasValue)
                    {
                        await UpdateCustomerOutstandingBalance(weighment.CustomerId.Value, weighment.TotalAmount.Value);
                    }

                    TempData["Success"] = $"Weighment {transactionNumber} created successfully.";
                    _logger.LogInformation("Weighment {TransactionNumber} created by user {UserName}", 
                        transactionNumber, User.Identity?.Name);
                    
                    return RedirectToAction(nameof(Index));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating weighment");
                ModelState.AddModelError("", "An error occurred while creating the weighment. Please try again.");
            }

            await PopulateDropdowns(model);
            return View(model);
        }

        // GET: Weighment/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var weighment = await _context.WeighmentTransactions.FindAsync(id);
            if (weighment == null)
            {
                return NotFound();
            }

            var model = new WeighmentEditViewModel
            {
                Id = weighment.Id,
                TransactionNumber = weighment.TransactionNumber,
                TransactionDate = weighment.TransactionDate,
                VehicleRegNumber = weighment.VehicleRegNumber,
                DriverName = weighment.DriverName,
                DriverPhone = weighment.DriverPhone,
                CustomerId = weighment.CustomerId,
                WeighbridgeId = weighment.WeighbridgeId,
                MaterialId = weighment.MaterialId,
                PricePerUnit = weighment.PricePerUnit,
                VatRate = weighment.VatRate,
                GrossWeight = weighment.GrossWeight,
                TareWeight = weighment.TareWeight,
                WeightUnit = weighment.WeightUnit,
                SubTotal = weighment.SubTotal,
                VatAmount = weighment.VatAmount,
                TotalAmount = weighment.TotalAmount,
                EntryTime = weighment.EntryTime,
                ExitTime = weighment.ExitTime,
                TransactionType = weighment.TransactionType,
                Status = weighment.Status,
                ChallanNumber = weighment.ChallanNumber
            };

            await PopulateDropdowns(model);
            return View(model);
        }

        // POST: Weighment/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, WeighmentEditViewModel model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            try
            {
                if (ModelState.IsValid)
                {
                    var weighment = await _context.WeighmentTransactions.FindAsync(id);
                    if (weighment == null)
                    {
                        return NotFound();
                    }

                    // Allow updating transaction number (ensure uniqueness)
                    if (!string.Equals(weighment.TransactionNumber, model.TransactionNumber, StringComparison.OrdinalIgnoreCase))
                    {
                        var exists = await _context.WeighmentTransactions.AnyAsync(w => w.TransactionNumber == model.TransactionNumber && w.Id != id);
                        if (exists)
                        {
                            ModelState.AddModelError("TransactionNumber", "Transaction number already exists.");
                            await PopulateDropdowns(model);
                            return View(model);
                        }
                        weighment.TransactionNumber = model.TransactionNumber;
                    }

                    // Store old values for comparison
                    var oldCustomerId = weighment.CustomerId;
                    var oldTotalAmount = weighment.TotalAmount;

                    // Update weighment
                    weighment.VehicleRegNumber = model.VehicleRegNumber;
                    weighment.DriverName = model.DriverName;
                    weighment.DriverPhone = model.DriverPhone;
                    weighment.CustomerId = model.CustomerId;
                    weighment.WeighbridgeId = model.WeighbridgeId;
                    weighment.MaterialId = model.MaterialId;
                    weighment.PricePerUnit = model.PricePerUnit;
                    weighment.VatRate = model.VatRate;
                    weighment.GrossWeight = model.GrossWeight;
                    weighment.TareWeight = model.TareWeight;
                    weighment.WeightUnit = model.WeightUnit;
                    weighment.EntryTime = model.EntryTime;
                    weighment.ExitTime = model.ExitTime;
                    weighment.TransactionType = model.TransactionType;
                    weighment.Status = model.Status;
                    weighment.ChallanNumber = model.ChallanNumber;
                    weighment.ModifiedBy = User.Identity?.Name;
                    weighment.ModifiedAt = DateTime.Now;

                    // Recalculate financials
                    weighment.CalculateFinancials();

                    await _context.SaveChangesAsync();

                    // Update customer outstanding balances if customer or amount changed
                    if (oldCustomerId != model.CustomerId || oldTotalAmount != model.TotalAmount)
                    {
                        // Revert old customer balance
                        if (oldCustomerId.HasValue && oldTotalAmount.HasValue)
                        {
                            await UpdateCustomerOutstandingBalance(oldCustomerId.Value, -oldTotalAmount.Value);
                        }
                        
                        // Apply new customer balance
                        if (model.CustomerId.HasValue && weighment.TotalAmount.HasValue)
                        {
                            await UpdateCustomerOutstandingBalance(model.CustomerId.Value, weighment.TotalAmount.Value);
                        }
                    }

                    TempData["Success"] = $"Weighment {weighment.TransactionNumber} updated successfully.";
                    _logger.LogInformation("Weighment {TransactionNumber} updated by user {UserName}", 
                        weighment.TransactionNumber, User.Identity?.Name);
                    
                    return RedirectToAction(nameof(Index));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating weighment");
                ModelState.AddModelError("", "An error occurred while updating the weighment. Please try again.");
            }

            await PopulateDropdowns(model);
            return View(model);
        }

        // GET: Weighment/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var weighment = await _context.WeighmentTransactions
                .Include(w => w.Customer)
                .Include(w => w.Material)
                .Include(w => w.Weighbridge)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (weighment == null)
            {
                return NotFound();
            }

            return View(weighment);
        }

        // GET: Weighment/Operations (Real-time weighbridge operations)
        public async Task<IActionResult> Operations()
        {
            try
            {
                var activeWeighments = await _context.WeighmentTransactions
                    .Include(w => w.Customer)
                    .Include(w => w.Material)
                    .Include(w => w.Weighbridge)
                    .Where(w => w.Status == "InProgress")
                    .OrderBy(w => w.EntryTime)
                    .ToListAsync();

                var completedToday = await _context.WeighmentTransactions
                    .Include(w => w.Customer)
                    .Include(w => w.Material)
                    .Where(w => w.Status == "Completed" && w.TransactionDate.Date == DateTime.Today)
                    .OrderByDescending(w => w.ExitTime)
                    .Take(10)
                    .ToListAsync();

                var viewModel = new WeighmentOperationsViewModel
                {
                    ActiveWeighments = activeWeighments,
                    CompletedToday = completedToday,
                    ActiveWeighbridges = await _context.Weighbridges
                        .Where(w => w.Status == "Active")
                        .ToListAsync()
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading weighment operations");
                return View(new WeighmentOperationsViewModel
                {
                    ErrorMessage = "An error occurred while loading operations data."
                });
            }
        }

        // AJAX: Get material price
        [HttpGet]
        public async Task<JsonResult> GetMaterialPrice(int materialId)
        {
            try
            {
                var material = await _context.Materials.FindAsync(materialId);
                if (material == null)
                {
                    return Json(new { success = false, message = "Material not found" });
                }

                return Json(new 
                { 
                    success = true, 
                    unitPrice = material.UnitPrice,
                    vatRate = material.VatRate
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting material price for material {MaterialId}", materialId);
                return Json(new { success = false, message = "Error retrieving material price" });
            }
        }

        // AJAX: Check customer credit
        [HttpGet]
        public async Task<JsonResult> CheckCustomerCredit(int customerId, decimal estimatedAmount)
        {
            try
            {
                var customer = await _context.Customers.FindAsync(customerId);
                if (customer == null)
                {
                    return Json(new { success = false, message = "Customer not found" });
                }

                var exceedsLimit = customer.HasExceededCreditLimit(estimatedAmount);
                var availableCredit = customer.AvailableCredit;

                return Json(new 
                { 
                    success = true, 
                    exceedsLimit, 
                    availableCredit,
                    currentOutstanding = customer.OutstandingBalance,
                    creditLimit = customer.CreditLimit
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking customer credit for customer {CustomerId}", customerId);
                return Json(new { success = false, message = "Error checking customer credit" });
            }
        }

        // AJAX: Generate transaction number
        [HttpGet]
        public async Task<JsonResult> GenerateTransactionNumber()
        {
            try
            {
                var transactionNumber = await GenerateNewTransactionNumber();
                return Json(new { success = true, transactionNumber });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating transaction number");
                return Json(new { success = false, message = "Error generating transaction number" });
            }
        }

        // Helper methods
        private async Task<string> GenerateNewTransactionNumber()
        {
            var today = DateTime.Today;
            var prefix = $"WB/NG/{today:yyyy}/";
            
            var lastTransaction = await _context.WeighmentTransactions
                .Where(w => w.TransactionNumber.StartsWith(prefix))
                .OrderByDescending(w => w.TransactionNumber)
                .FirstOrDefaultAsync();

            int nextNumber = 1;
            if (lastTransaction != null)
            {
                var lastNumberStr = lastTransaction.TransactionNumber.Substring(prefix.Length);
                if (int.TryParse(lastNumberStr, out int lastNumber))
                {
                    nextNumber = lastNumber + 1;
                }
            }

            return $"{prefix}{nextNumber:D4}";
        }

        private async Task PopulateDropdowns(WeighmentCreateViewModel model)
        {
            model.Customers = await _context.Customers
                .Where(c => c.Status == "Active")
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = $"{c.Name} - {c.ContactPerson}"
                })
                .ToListAsync();

            model.Materials = await _context.Materials
                .Where(m => m.Status == "Active")
                .Select(m => new SelectListItem
                {
                    Value = m.Id.ToString(),
                    Text = $"{m.Name} ({m.Type}) - ₦{m.UnitPrice:N2}/ton"
                })
                .ToListAsync();

            model.Weighbridges = await _context.Weighbridges
                .Where(w => w.Status == "Active")
                .Select(w => new SelectListItem
                {
                    Value = w.Id.ToString(),
                    Text = $"{w.Name} - {w.Location}"
                })
                .ToListAsync();

            model.TransactionTypes = new List<SelectListItem>
            {
                new SelectListItem { Value = "Sales", Text = "Sales" },
                new SelectListItem { Value = "Purchase", Text = "Purchase" },
                new SelectListItem { Value = "Transfer", Text = "Transfer" }
            };

            model.Statuses = GetWeighmentStatuses();
        }

        private List<SelectListItem> GetWeighmentStatuses()
        {
            return new List<SelectListItem>
            {
                new SelectListItem { Value = "InProgress", Text = "In Progress" },
                new SelectListItem { Value = "Completed", Text = "Completed" },
                new SelectListItem { Value = "Cancelled", Text = "Cancelled" }
            };
        }

        private decimal CalculateEstimatedAmount(WeighmentCreateViewModel model)
        {
            if (model.GrossWeight > 0 && model.PricePerUnit.HasValue)
            {
                var netWeight = model.GrossWeight - (model.TareWeight ?? 0);
                var quantityInTons = model.WeightUnit == "kg" ? netWeight / 1000 : netWeight;
                var subtotal = quantityInTons * model.PricePerUnit.Value;
                var vatAmount = subtotal * (model.VatRate / 100);
                return subtotal + vatAmount;
            }
            return 0;
        }

        private async Task UpdateCustomerOutstandingBalance(int customerId, decimal amount)
        {
            var customer = await _context.Customers.FindAsync(customerId);
            if (customer != null)
            {
                customer.OutstandingBalance += amount;
                await _context.SaveChangesAsync();
            }
        }

        // GET: Weighment/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var weighment = await _context.WeighmentTransactions
                .Include(w => w.Customer)
                .Include(w => w.Material)
                .Include(w => w.Weighbridge)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (weighment == null)
            {
                return NotFound();
            }

            if (weighment.IsInvoiced || weighment.Status == "Completed")
            {
                TempData["Error"] = "Cannot delete a completed or invoiced weighment.";
                return RedirectToAction(nameof(Index));
            }

            return View(weighment);
        }

        // POST: Weighment/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var weighment = await _context.WeighmentTransactions.FindAsync(id);
            if (weighment == null)
            {
                return NotFound();
            }

            if (weighment.IsInvoiced || weighment.Status == "Completed")
            {
                TempData["Error"] = "Cannot delete a completed or invoiced weighment.";
                return RedirectToAction(nameof(Index));
            }

            // Revert customer outstanding balance if applicable
            if (weighment.CustomerId.HasValue && weighment.TotalAmount.HasValue)
            {
                await UpdateCustomerOutstandingBalance(weighment.CustomerId.Value, -weighment.TotalAmount.Value);
            }

            _context.WeighmentTransactions.Remove(weighment);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Weighment {weighment.TransactionNumber} deleted successfully.";
            _logger.LogInformation("Weighment {TransactionNumber} deleted by user {UserName}", weighment.TransactionNumber, User.Identity?.Name);

            return RedirectToAction(nameof(Index));
        }
    }
}