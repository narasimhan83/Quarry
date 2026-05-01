using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuarryManagementSystem.Data;
using QuarryManagementSystem.Models.Domain;
using QuarryManagementSystem.Services;
using QuarryManagementSystem.ViewModels;
using System.Linq.Expressions;

namespace QuarryManagementSystem.Controllers
{
    [Authorize(Roles = "Admin,Manager,Accountant,Operator")]
    public class WeighmentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<WeighmentController> _logger;
        private readonly ICustomerPricingService _pricingService;

        public WeighmentController(
            ApplicationDbContext context,
            ILogger<WeighmentController> logger,
            ICustomerPricingService pricingService)
        {
            _context = context;
            _logger = logger;
            _pricingService = pricingService;
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
                    WeightUnit = "Ton",
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
                // Vehicle registration must come from the selected customer's
                // truck list. Walk-in / no-customer weighments are blocked here
                // because the form's truck dropdown is driven entirely by the
                // chosen customer — there is no way to pick a plate without one.
                await ValidateVehicleRegAgainstCustomerAsync(model.CustomerId, model.VehicleRegNumber);

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
                        // Quarry flow: Tare + Gross come from the weighbridge.
                        // Net = Gross − Tare is derived in CalculateFinancials.
                        GrossWeight = model.GrossWeight,
                        TareWeight = model.TareWeight,
                        WeightUnit = model.WeightUnit,
                        EntryTime = model.EntryTime,
                        ExitTime = model.ExitTime,
                        // TransactionType and ChallanNumber were removed from the
                        // form. TransactionType defaults to "Sales" on the entity;
                        // ChallanNumber stays null. Both columns remain in the DB
                        // for legacy data and may be reintroduced if needed.
                        Status = model.Status,
                        SelectedPrepaymentId = model.SelectedPrepaymentId,
                        SelectedPrepaymentLineItemId = model.SelectedPrepaymentLineItemId,
                        CreatedBy = User.Identity?.Name,
                        CreatedAt = DateTime.Now
                    };

                    // Compute financials honoring the customer's VAT treatment.
                    // Must happen BEFORE Add/SaveChanges so the stored Subtotal,
                    // VAT, and Total reflect Inclusive vs Exclusive math. We can't
                    // rely on weighment.CalculateFinancials() because the domain
                    // model doesn't know the customer's VAT type — only this
                    // controller does.
                    await ApplyVatTreatmentAsync(weighment, model.CustomerId);

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

                    // Redirect to the thermal-printer-friendly slip view with
                    // autoprint=true so the browser's print dialog opens
                    // automatically. User can dismiss to stay on the slip page
                    // or print to their 80mm printer. From there they can
                    // navigate back to Index via the back button.
                    return RedirectToAction(nameof(PrintSlip80), new { id = weighment.Id, autoprint = true });
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
                RebateAmount = weighment.RebateAmount,
                TotalAmount = weighment.TotalAmount,
                EntryTime = weighment.EntryTime,
                ExitTime = weighment.ExitTime,
                TransactionType = weighment.TransactionType,
                Status = weighment.Status,
                ChallanNumber = weighment.ChallanNumber,
                SelectedPrepaymentId = weighment.SelectedPrepaymentId,
                SelectedPrepaymentLineItemId = weighment.SelectedPrepaymentLineItemId
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
                // Same guard as Create: the truck has to belong to the chosen
                // customer (and a customer must be chosen).
                await ValidateVehicleRegAgainstCustomerAsync(model.CustomerId, model.VehicleRegNumber);

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
                    // TransactionType and ChallanNumber removed from the form.
                    // Don't overwrite the existing DB values with the null that
                    // arrives from unposted inputs. Existing rows keep their
                    // saved values; new rows default TransactionType to "Sales".
                    weighment.Status = model.Status;
                    weighment.ModifiedBy = User.Identity?.Name;
                    weighment.ModifiedAt = DateTime.Now;
                    weighment.SelectedPrepaymentId = model.SelectedPrepaymentId;
                    weighment.SelectedPrepaymentLineItemId = model.SelectedPrepaymentLineItemId;

                    // Recalculate financials honoring the customer's VAT treatment.
                    await ApplyVatTreatmentAsync(weighment, model.CustomerId);

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

        // GET: Weighment/PrintSlip80/5
        //
        // Renders a 3-copy dispatch slip sized for 80mm thermal printers:
        //   1. Customer Copy   — with signature line
        //   2. Driver Copy     — with 'Received' acknowledgement
        //   3. Quarry Copy     — full operator record
        //
        // Each copy prints on its own page (page-break-after: always) so a roll
        // printer cuts between them. Query string ?autoprint=1 triggers window.print()
        // on load — used by the Create redirect so the operator gets the print
        // dialog immediately after saving a new weighment.
        public async Task<IActionResult> PrintSlip80(int? id, bool autoprint = false)
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

            ViewData["AutoPrint"] = autoprint;
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
        //
        // When a customerId is supplied, defer to the pricing service which will
        // honor per-customer price history and fall back to the catalog when no
        // custom price is on file. Without a customer, return the raw material
        // catalog price (legacy behavior).
        [HttpGet]
        public async Task<JsonResult> GetMaterialPrice(int materialId, int? customerId = null)
        {
            try
            {
                if (customerId.HasValue && customerId.Value > 0)
                {
                    var pricing = await _pricingService.GetPricingAsync(customerId.Value, materialId);

                    // Pull the customer's rebate setting too — the weighment form
                    // multiplies it by net tons and shows it as a separate row in
                    // the Financial Summary so the operator can see the discount
                    // before saving. Server-side ApplyVatTreatmentAsync re-derives
                    // these values from the same source on POST.
                    var customerRebate = await _context.Customers
                        .Where(c => c.Id == customerId.Value)
                        .Select(c => new { c.HasRebate, c.RebateAmount })
                        .FirstOrDefaultAsync();

                    return Json(new
                    {
                        success = true,
                        unitPrice = pricing.UnitPrice,
                        vatRate = pricing.VatRate,
                        isCustomerSpecific = pricing.IsCustomerSpecific,
                        vatType = pricing.VatType,
                        hasRebate = customerRebate?.HasRebate ?? false,
                        rebateAmount = customerRebate?.RebateAmount ?? 0m
                    });
                }

                var material = await _context.Materials.FindAsync(materialId);
                if (material == null)
                {
                    return Json(new { success = false, message = "Material not found" });
                }

                return Json(new
                {
                    success = true,
                    unitPrice = material.UnitPrice,
                    vatRate = material.VatRate,
                    isCustomerSpecific = false,
                    vatType = (string?)null,
                    hasRebate = false,
                    rebateAmount = 0m
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting material price for material {MaterialId}", materialId);
                return Json(new { success = false, message = "Error retrieving material price" });
            }
        }

        // AJAX: List active prepayments with remaining balance for a customer.
        // Populates the Prepayment dropdown on the Weighment Create / Edit form.
        // Returns only prepayments that still have funds left (Amount > UsedAmount);
        // exhausted / cancelled prepayments are filtered out.
        [HttpGet]
        public async Task<JsonResult> GetCustomerPrepayments(int customerId)
        {
            try
            {
                var prepayments = await _context.CustomerPrepayments
                    .Where(p => p.CustomerId == customerId && p.Status == "Active")
                    .OrderBy(p => p.PrepaymentDate)
                    .Select(p => new
                    {
                        id = p.Id,
                        number = p.PrepaymentNumber,
                        date = p.PrepaymentDate,
                        amount = p.Amount,
                        used = p.UsedAmount,
                        remaining = p.Amount - p.UsedAmount
                    })
                    .ToListAsync();

                // Shape for the dropdown: show number + remaining balance for easy selection.
                var shaped = prepayments
                    .Where(p => p.remaining > 0)
                    .Select(p => new
                    {
                        id = p.id,
                        number = p.number,
                        date = p.date.ToString("dd/MM/yyyy"),
                        amount = p.amount,
                        remaining = p.remaining,
                        label = $"{p.number} \u2014 \u20a6{p.remaining:N2} remaining (of \u20a6{p.amount:N2})"
                    })
                    .ToList();

                return Json(new { success = true, prepayments = shaped });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing prepayments for customer {CustomerId}", customerId);
                return Json(new { success = false, message = "Error loading prepayments" });
            }
        }

        // AJAX: List trucks registered to a customer. The Vehicle Registration
        // field on Create / Edit is a strict dropdown driven by this endpoint —
        // only trucks that belong to the chosen customer can be selected.
        // We include inactive trucks but flag them in the label, since some
        // existing data was saved with IsActive defaulted to false (a legacy
        // form-binding quirk) and operators still need to pick those plates.
        // Filter clients can hide inactive ones if they want.
        [HttpGet]
        public async Task<JsonResult> GetCustomerTrucks(int customerId)
        {
            try
            {
                var trucks = await _context.CustomerTrucks
                    .Where(t => t.CustomerId == customerId)
                    .OrderByDescending(t => t.IsActive)
                    .ThenBy(t => t.CustomerTruckNumber)
                    .Select(t => new
                    {
                        id = t.CustomerTruckId,
                        number = t.CustomerTruckNumber,
                        isActive = t.IsActive
                    })
                    .ToListAsync();

                return Json(new { success = true, trucks });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing trucks for customer {CustomerId}", customerId);
                return Json(new { success = false, message = "Error loading trucks" });
            }
        }

        // AJAX: Get the line items of a specific prepayment, with per-line remaining
        // quantity / amount. The weighment form uses this to populate a secondary
        // material dropdown once a prepayment is picked — so the operator sees
        // exactly which materials were prepaid and how much is left on each.
        [HttpGet]
        public async Task<JsonResult> GetPrepaymentLineItems(int prepaymentId)
        {
            try
            {
                var prepayment = await _context.CustomerPrepayments
                    .Include(p => p.LineItems)
                        .ThenInclude(li => li.Material)
                    .FirstOrDefaultAsync(p => p.Id == prepaymentId);

                if (prepayment == null)
                {
                    return Json(new { success = false, message = "Prepayment not found" });
                }

                // Build in memory (not via IQueryable) because the label format
                // uses interpolation that won't translate cleanly to SQL.
                var lines = prepayment.LineItems
                    .Where(li => (li.LineTotal - li.UsedAmount) > 0)
                    .OrderBy(li => li.Id)
                    .Select(li =>
                    {
                        var matName = li.Material != null ? li.Material.Name : "(material)";
                        var remainingQty = li.Quantity - li.UsedQuantity;
                        return new
                        {
                            id = li.Id,
                            materialId = li.MaterialId,
                            materialName = matName,
                            unit = li.Unit,
                            unitPrice = li.UnitPrice,
                            remainingQty = remainingQty,
                            remainingAmount = li.LineTotal - li.UsedAmount,
                            // Note: VatAmount on line is audit-only; the catalog / customer
                            // VAT rate still drives the weighment's VAT rate. Exposed here
                            // in case the UI wants to show the effective rate baked in.
                            vatAmount = li.VatAmount,
                            rebateAmount = li.RebateAmount,
                            label = string.Format(
                                "{0} \u2014 {1:N2} {2} remaining @ \u20a6{3:N2}/{2}",
                                matName, remainingQty, li.Unit, li.UnitPrice)
                        };
                    })
                    .ToList();

                return Json(new { success = true, prepaymentNumber = prepayment.PrepaymentNumber, lines });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting line items for prepayment {PrepaymentId}", prepaymentId);
                return Json(new { success = false, message = "Error loading prepayment line items" });
            }
        }

        // AJAX: Check customer credit
        //
        // Mirrors InvoiceController.CheckCustomerCredit so both the weighment
        // and invoice screens show the same numbers. Key rules:
        //   • currentOutstanding is floored at 0 — negatives are corruption
        //     or overpayment, not "customer owes us negative money".
        //   • projectedOutstanding drains the prepayment wallet first, then
        //     only the shortfall lands on AR.
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

                // Floor raw outstanding at 0 for display. See InvoiceController
                // for the full rationale — same logic lives there.
                var rawOutstanding = customer.OutstandingBalance;
                var currentOutstanding = rawOutstanding < 0 ? 0m : rawOutstanding;

                // Include prepayment wallet in effective exposure
                var prepaymentBalance = await GetAvailablePrepaymentAsync(customerId);

                var effectiveOutstanding = currentOutstanding - prepaymentBalance;
                if (effectiveOutstanding < 0)
                {
                    effectiveOutstanding = 0;
                }

                // Projected: wallet covers current outstanding first, then the
                // new estimated amount; only the shortfall adds to AR.
                var walletUsedForCurrent = Math.Min(currentOutstanding, prepaymentBalance);
                var walletAfterCurrent  = prepaymentBalance - walletUsedForCurrent;
                var shortfall           = Math.Max(0m, estimatedAmount - walletAfterCurrent);
                var projectedOutstanding = effectiveOutstanding + shortfall;

                var exceedsLimit = projectedOutstanding > customer.CreditLimit;
                var availableCredit = customer.AvailableCredit;

                return Json(new
                {
                    success = true,
                    exceedsLimit,
                    availableCredit,
                    currentOutstanding,
                    rawOutstanding,
                    creditLimit = customer.CreditLimit,
                    prepaymentBalance,
                    effectiveOutstanding,
                    projectedOutstanding
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

        /// <summary>
        /// Validates that the posted VehicleRegNumber belongs to the chosen
        /// customer's active truck list. The Create / Edit form's truck field
        /// is a strict dropdown driven by GetCustomerTrucks, so under normal
        /// use this is a no-op — but a tampered POST or stale form could
        /// still smuggle a wrong plate through, hence the server-side check.
        /// Walk-in weighments (no customer) are also rejected because trucks
        /// can only be picked once a customer is chosen.
        /// </summary>
        private async Task ValidateVehicleRegAgainstCustomerAsync(int? customerId, string? vehicleRegNumber)
        {
            if (!customerId.HasValue)
            {
                ModelState.AddModelError(nameof(WeighmentCreateViewModel.CustomerId),
                    "Customer is required so the truck can be validated against the customer's registered fleet.");
                return;
            }
            if (string.IsNullOrWhiteSpace(vehicleRegNumber))
            {
                ModelState.AddModelError(nameof(WeighmentCreateViewModel.VehicleRegNumber),
                    "Vehicle registration is required — pick one of the customer's registered trucks.");
                return;
            }

            var trimmed = vehicleRegNumber.Trim();
            // Match on customer + plate only; do not require IsActive. The
            // checkbox-binding bug we hit early in the rollout left some
            // valid trucks saved with IsActive = false. Filtering on it here
            // would block saves of trucks the operator can clearly see in
            // the dropdown. Once that data is cleaned up, this filter can be
            // tightened back to require IsActive.
            var matches = await _context.CustomerTrucks.AnyAsync(t =>
                t.CustomerId == customerId.Value &&
                t.CustomerTruckNumber == trimmed);

            if (!matches)
            {
                ModelState.AddModelError(nameof(WeighmentCreateViewModel.VehicleRegNumber),
                    "This vehicle is not registered to the selected customer. Add the truck to the customer first, or pick a registered one.");
            }
        }

        /// <summary>
        /// Populates SubTotal / VatAmount / RebateAmount / TotalAmount on the
        /// weighment using the customer's VAT treatment and rebate settings.
        /// Mirrors the Invoice / Quotation logic so the saved numbers match the
        /// UI (Financial Summary panel) and downstream documents (PrintSlip80).
        /// <para/>
        /// Exclusive (default): Subtotal = NetTons × Price; VAT = Subtotal × rate;
        /// Total = Subtotal + VAT − Rebate.
        /// <para/>
        /// Inclusive: Price already embeds VAT. LineGross = NetTons × Price;
        /// VAT = LineGross × rate / (100 + rate); Subtotal = LineGross − VAT;
        /// Total = LineGross − Rebate (VAT is not added again, but rebate still reduces).
        /// <para/>
        /// Rebate = customer.RebateAmount (per-unit) × NetTons. Capped at
        /// Subtotal + VAT so a line never goes negative.
        /// </summary>
        private async Task ApplyVatTreatmentAsync(WeighmentTransaction weighment, int? customerId)
        {
            // Recompute NetWeight = Gross − Tare and PERSIST it to the entity.
            // Critical: NetWeight is a stored column on WeighmentTransaction, not
            // a computed property. If we don't assign it here, the list view and
            // any downstream consumer will see 0 even when Gross and Tare are
            // populated — which is exactly what caused the "Net (kg) = 0" bug.
            // Floor at 0 so a Tare-only first-weighing record doesn't go negative.
            var net = weighment.GrossWeight - (weighment.TareWeight ?? 0m);
            if (net < 0) net = 0;
            weighment.NetWeight = net;

            // Derive billable quantity (Net in tons). The weighment stores Gross /
            // Tare / Net in whatever WeightUnit the operator picked — typically
            // "Ton" on new records, "kg" on legacy. Convert only for kg.
            var quantityInTons = weighment.WeightUnit == "kg" ? net / 1000m : net;

            if (quantityInTons <= 0 || !weighment.PricePerUnit.HasValue)
            {
                weighment.SubTotal = 0;
                weighment.VatAmount = 0;
                weighment.RebateAmount = 0;
                weighment.TotalAmount = 0;
                return;
            }

            var lineGross = quantityInTons * weighment.PricePerUnit.Value;
            var rate = weighment.VatRate;

            // Look up VAT type + rebate from the customer (if any). No customer →
            // no VAT separation, no rebate. Same default as the client-side
            // calculateFinancials() in Weighment Create / Edit views.
            string vatType = string.Empty;
            decimal perUnitRebate = 0m;
            if (customerId.HasValue)
            {
                var customer = await _context.Customers
                    .Include(c => c.VatType)
                    .FirstOrDefaultAsync(c => c.Id == customerId.Value);
                if (customer?.VatType?.Name != null)
                {
                    vatType = customer.VatType.Name;
                }
                if (customer != null && customer.HasRebate)
                {
                    perUnitRebate = customer.RebateAmount ?? 0m;
                }
            }

            // VAT is only computed for Exclusive customers. For Inclusive (or
            // no VAT type set, or no customer), VAT is not separated out at
            // all — the price IS the price. Subtotal = full line gross,
            // VatAmount = 0. Mirrors the UI behaviour exactly so the saved
            // numbers match what the operator saw on screen at submit time.
            // Use a Contains check so labels like "Exclusive(Paid By Customer)"
            // still match — the seeded names aren't always exactly "Exclusive".
            decimal subtotal;
            decimal vat;
            var isExclusive = vatType.IndexOf("Exclusive", StringComparison.OrdinalIgnoreCase) >= 0;
            if (isExclusive)
            {
                subtotal = Math.Round(lineGross, 2);
                vat = Math.Round(subtotal * (rate / 100m), 2);
            }
            else
            {
                subtotal = Math.Round(lineGross, 2);
                vat = 0m;
            }

            // Rebate scales with quantity. Cap at Subtotal + VAT so the line
            // can't go negative even if the customer's per-unit rebate happens
            // to exceed the unit price for some material.
            var rebate = Math.Round(perUnitRebate * quantityInTons, 2);
            var maxRebate = subtotal + vat;
            if (rebate > maxRebate) rebate = maxRebate;

            weighment.SubTotal = subtotal;
            weighment.VatAmount = vat;
            weighment.RebateAmount = rebate;
            weighment.TotalAmount = Math.Round(subtotal + vat - rebate, 2);
        }

        private async Task UpdateCustomerOutstandingBalance(int customerId, decimal amount)
        {
            var customer = await _context.Customers.FindAsync(customerId);
            if (customer != null)
            {
                customer.OutstandingBalance += amount;
                // Safety floor: a customer's outstanding balance should never be
                // negative. If the revert-and-reapply pair in the caller ends up
                // below zero (typically because the old TotalAmount being reverted
                // is stale — e.g. the original record had NetWeight = 0 and so
                // TotalAmount = 0 didn't really get added, but we now revert a
                // non-zero value), clamp to zero rather than propagate corruption.
                // The authoritative figure is recomputable from invoices; see
                // SQL/Reconcile_CustomerOutstandingBalance.sql for the full rebuild.
                if (customer.OutstandingBalance < 0)
                {
                    customer.OutstandingBalance = 0;
                }
                await _context.SaveChangesAsync();
            }
        }

        private async Task<decimal> GetAvailablePrepaymentAsync(int customerId)
        {
            var prepayments = await _context.CustomerPrepayments
                .Where(p => p.CustomerId == customerId && p.Status == "Active")
                .ToListAsync();

            return prepayments.Sum(p => p.Amount - p.UsedAmount);
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