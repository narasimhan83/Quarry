using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuarryManagementSystem.Data;
using QuarryManagementSystem.Models;
using QuarryManagementSystem.Models.Domain;
using QuarryManagementSystem.Services;
using QuarryManagementSystem.Utilities;
using QuarryManagementSystem.ViewModels;
using System.Security.Claims;

namespace QuarryManagementSystem.Controllers
{
    [Authorize(Roles = "Admin,Manager,Accountant")]
    public class PrepaymentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<PrepaymentController> _logger;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ICustomerPricingService _pricingService;

        public PrepaymentController(
            ApplicationDbContext context,
            ILogger<PrepaymentController> logger,
            UserManager<ApplicationUser> userManager,
            ICustomerPricingService pricingService)
        {
            _context = context;
            _logger = logger;
            _userManager = userManager;
            _pricingService = pricingService;
        }

        // ----------------------------------------------------------------------
        // GET: Prepayment
        // ----------------------------------------------------------------------
        public async Task<IActionResult> Index(int? customerId, DateTime? fromDate, DateTime? toDate)
        {
            var query = _context.CustomerPrepayments
                .Include(p => p.Customer)
                .Include(p => p.LineItems)
                    .ThenInclude(li => li.Material)
                .Include(p => p.Applications)
                .AsQueryable();

            if (customerId.HasValue)
            {
                query = query.Where(p => p.CustomerId == customerId.Value);
            }
            if (fromDate.HasValue)
            {
                query = query.Where(p => p.PrepaymentDate >= fromDate.Value);
            }
            if (toDate.HasValue)
            {
                query = query.Where(p => p.PrepaymentDate <= toDate.Value);
            }

            var prepayments = await query
                .OrderByDescending(p => p.PrepaymentDate)
                .ThenByDescending(p => p.Id)
                .ToListAsync();

            // Keep the top-level UsedAmount in sync with its applications so the list
            // view is always accurate even if something was missed upstream.
            bool anyUpdated = false;
            foreach (var p in prepayments)
            {
                var appliedTotal = p.Applications?.Sum(a => a.AppliedAmount) ?? 0m;
                if (appliedTotal != p.UsedAmount)
                {
                    p.UsedAmount = appliedTotal;
                    p.UpdatedAt = DateTime.Now;
                    p.UpdatedBy = User.Identity?.Name;
                    anyUpdated = true;

                    if (p.Amount - p.UsedAmount <= 0 && p.Status == "Active")
                    {
                        p.Status = "Exhausted";
                    }
                }
            }
            if (anyUpdated)
            {
                await _context.SaveChangesAsync();
            }

            ViewBag.Customers = await _context.Customers
                .OrderBy(c => c.Name)
                .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name })
                .ToListAsync();

            return View(prepayments);
        }

        // ----------------------------------------------------------------------
        // GET: Prepayment/Create
        // ----------------------------------------------------------------------
        public async Task<IActionResult> Create()
        {
            var model = new PrepaymentCreateEditViewModel
            {
                PrepaymentDate = DateTime.Now
            };
            await PopulateDropdownsAsync(model);
            return View(model);
        }

        // ----------------------------------------------------------------------
        // POST: Prepayment/Create
        // ----------------------------------------------------------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PrepaymentCreateEditViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await PopulateDropdownsAsync(model);
                return View(model);
            }

            if (!model.CustomerId.HasValue || model.CustomerId <= 0)
            {
                ModelState.AddModelError(nameof(model.CustomerId), "Please select a customer.");
                await PopulateDropdownsAsync(model);
                return View(model);
            }

            // Either-or: a Direct Amount entry skips line items entirely;
            // line items skip Direct Amount. Both filled = ambiguous and rejected.
            var hasDirectAmount = model.DirectAmount.HasValue && model.DirectAmount.Value > 0m;
            var rawValidLines = (model.Items ?? new List<PrepaymentLineItemInput>())
                .Where(li => li.MaterialId.HasValue && li.MaterialId > 0 && li.Quantity > 0 && li.UnitPrice > 0)
                .ToList();
            var hasLineItems = rawValidLines.Any();

            if (hasDirectAmount && hasLineItems)
            {
                ModelState.AddModelError(string.Empty, "Enter either a Direct Amount OR line items, not both. Clear one of them and try again.");
                await PopulateDropdownsAsync(model);
                return View(model);
            }

            if (!hasDirectAmount && !hasLineItems)
            {
                ModelState.AddModelError(string.Empty, "Enter a Direct Amount, or add at least one line item with material, quantity and unit price.");
                await PopulateDropdownsAsync(model);
                return View(model);
            }

            // Direct Amount path: save a flat deposit with no line items.
            // VAT / rebate are recognised at invoice time when this prepayment
            // is drawn down, not at deposit time — the same way real
            // accounting treats customer deposits.
            if (hasDirectAmount)
            {
                using var tx = await _context.Database.BeginTransactionAsync();
                try
                {
                    var directPrepayment = new CustomerPrepayment
                    {
                        PrepaymentNumber = await GeneratePrepaymentNumberAsync(),
                        CustomerId = model.CustomerId.Value,
                        PrepaymentDate = model.PrepaymentDate,
                        ExpectedPickupDate = model.ExpectedPickupDate,
                        PaymentMethod = await ResolvePaymentMethodNameAsync(model.PaymentMethodId, model.PaymentMethod),
                        PaymentMethodId = model.PaymentMethodId,
                        Reference = model.Reference,
                        Notes = model.Notes,
                        Status = "Active",
                        Amount = Math.Round(model.DirectAmount!.Value, 2),
                        UsedAmount = 0m,
                        CreatedAt = DateTime.Now,
                        CreatedBy = User.Identity?.Name
                    };

                    _context.CustomerPrepayments.Add(directPrepayment);
                    await _context.SaveChangesAsync();

                    await CreatePrepaymentJournalEntryAsync(directPrepayment);

                    await tx.CommitAsync();

                    TempData["Success"] = $"Prepayment {directPrepayment.PrepaymentNumber} created. Direct deposit ₦{directPrepayment.Amount:N2}.";
                    _logger.LogInformation("Prepayment {PrepaymentNumber} (direct amount) created for customer {CustomerId} by {UserName}",
                        directPrepayment.PrepaymentNumber, directPrepayment.CustomerId, User.Identity?.Name);
                    return RedirectToAction(nameof(Details), new { id = directPrepayment.Id });
                }
                catch (Exception ex)
                {
                    await tx.RollbackAsync();
                    _logger.LogError(ex, "Error creating direct-amount prepayment. Transaction rolled back.");
                    ModelState.AddModelError(string.Empty, "An error occurred while creating the prepayment. Please try again.");
                    await PopulateDropdownsAsync(model);
                    return View(model);
                }
            }

            // Line-items path (existing behaviour).
            var validLines = rawValidLines;

            // Compute the VAT / rebate breakdown for each line from the posted
            // raw UnitPrice and the customer's current settings. UnitPrice stays
            // as-is (customer's catalog price); VatAmount and RebateAmount are
            // filled in per line. LineTotal below uses all three.
            await ComputeLineBreakdownAsync(model.CustomerId.Value, validLines);

            // Line total model (new):
            //   LineTotal = qty × UnitPrice + VatAmount − RebateAmount
            // where VatAmount and RebateAmount have already been scaled to the
            // line quantity by ComputeLineBreakdownAsync.
            foreach (var li in validLines)
            {
                li.LineTotal = Math.Round(li.Quantity * li.UnitPrice + li.VatAmount - li.RebateAmount, 2);
            }
            model.TotalAmount = validLines.Sum(li => li.LineTotal);

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var prepayment = new CustomerPrepayment
                {
                    PrepaymentNumber = await GeneratePrepaymentNumberAsync(),
                    CustomerId = model.CustomerId.Value,
                    PrepaymentDate = model.PrepaymentDate,
                    ExpectedPickupDate = model.ExpectedPickupDate,
                    PaymentMethod = await ResolvePaymentMethodNameAsync(model.PaymentMethodId, model.PaymentMethod),
                    PaymentMethodId = model.PaymentMethodId,
                    Reference = model.Reference,
                    Notes = model.Notes,
                    Status = "Active",
                    Amount = model.TotalAmount,
                    UsedAmount = 0m,
                    CreatedAt = DateTime.Now,
                    CreatedBy = User.Identity?.Name
                };

                foreach (var li in validLines)
                {
                    prepayment.LineItems.Add(new PrepaymentLineItem
                    {
                        MaterialId = li.MaterialId!.Value,
                        Quantity = li.Quantity,
                        Unit = string.IsNullOrWhiteSpace(li.Unit) ? "Ton" : li.Unit,
                        UnitPrice = li.UnitPrice,
                        VatAmount = li.VatAmount,
                        RebateAmount = li.RebateAmount,
                        LineTotal = li.LineTotal,
                        UsedQuantity = 0m,
                        UsedAmount = 0m,
                        CreatedAt = DateTime.Now
                    });
                }

                _context.CustomerPrepayments.Add(prepayment);
                await _context.SaveChangesAsync();

                await CreatePrepaymentJournalEntryAsync(prepayment);

                await transaction.CommitAsync();

                TempData["Success"] = $"Prepayment {prepayment.PrepaymentNumber} created successfully with {validLines.Count} line item(s). Total: ₦{prepayment.Amount:N2}.";
                _logger.LogInformation("Prepayment {PrepaymentNumber} created with {Count} lines for customer {CustomerId} by {UserName}",
                    prepayment.PrepaymentNumber, validLines.Count, prepayment.CustomerId, User.Identity?.Name);

                return RedirectToAction(nameof(Details), new { id = prepayment.Id });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error creating prepayment. The transaction was rolled back.");
                ModelState.AddModelError(string.Empty, "An error occurred while creating the prepayment. Please try again.");
                await PopulateDropdownsAsync(model);
                return View(model);
            }
        }

        // ----------------------------------------------------------------------
        // GET: Prepayment/Edit/5
        // ----------------------------------------------------------------------
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var prepayment = await _context.CustomerPrepayments
                .Include(p => p.LineItems)
                    .ThenInclude(li => li.Material)
                .Include(p => p.Applications)
                .FirstOrDefaultAsync(p => p.Id == id.Value);

            if (prepayment == null) return NotFound();

            if (prepayment.Applications != null && prepayment.Applications.Any())
            {
                TempData["Error"] = "This prepayment has already been applied to one or more invoices and can no longer be edited. Use Cancel instead.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var model = MapToEditViewModel(prepayment);
            await PopulateDropdownsAsync(model);
            return View(model);
        }

        // ----------------------------------------------------------------------
        // POST: Prepayment/Edit/5
        // ----------------------------------------------------------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, PrepaymentCreateEditViewModel model)
        {
            if (id != model.Id) return NotFound();

            var prepayment = await _context.CustomerPrepayments
                .Include(p => p.LineItems)
                .Include(p => p.Applications)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (prepayment == null) return NotFound();

            if (prepayment.Applications != null && prepayment.Applications.Any())
            {
                TempData["Error"] = "This prepayment has already been applied to one or more invoices and can no longer be edited.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (!ModelState.IsValid)
            {
                await PopulateDropdownsAsync(model);
                return View(model);
            }

            if (!model.CustomerId.HasValue || model.CustomerId <= 0)
            {
                ModelState.AddModelError(nameof(model.CustomerId), "Please select a customer.");
                await PopulateDropdownsAsync(model);
                return View(model);
            }

            // Either-or, same as Create: Direct Amount XOR line items.
            var hasDirectAmount = model.DirectAmount.HasValue && model.DirectAmount.Value > 0m;
            var rawValidLines = (model.Items ?? new List<PrepaymentLineItemInput>())
                .Where(li => li.MaterialId.HasValue && li.MaterialId > 0 && li.Quantity > 0 && li.UnitPrice > 0)
                .ToList();
            var hasLineItems = rawValidLines.Any();

            if (hasDirectAmount && hasLineItems)
            {
                ModelState.AddModelError(string.Empty, "Enter either a Direct Amount OR line items, not both. Clear one of them and try again.");
                await PopulateDropdownsAsync(model);
                return View(model);
            }
            if (!hasDirectAmount && !hasLineItems)
            {
                ModelState.AddModelError(string.Empty, "Enter a Direct Amount, or add at least one line item with material, quantity and unit price.");
                await PopulateDropdownsAsync(model);
                return View(model);
            }

            // Direct Amount path: persist a flat deposit. Wipe any pre-existing
            // line items the prepayment used to carry (operator switched modes).
            if (hasDirectAmount)
            {
                using var dirTx = await _context.Database.BeginTransactionAsync();
                try
                {
                    prepayment.CustomerId = model.CustomerId.Value;
                    prepayment.PrepaymentDate = model.PrepaymentDate;
                    prepayment.ExpectedPickupDate = model.ExpectedPickupDate;
                    prepayment.PaymentMethod = await ResolvePaymentMethodNameAsync(model.PaymentMethodId, model.PaymentMethod);
                    prepayment.PaymentMethodId = model.PaymentMethodId;
                    prepayment.Reference = model.Reference;
                    prepayment.Notes = model.Notes;
                    prepayment.Status = string.IsNullOrWhiteSpace(model.Status) ? prepayment.Status : model.Status;
                    prepayment.Amount = Math.Round(model.DirectAmount!.Value, 2);
                    prepayment.UpdatedAt = DateTime.Now;
                    prepayment.UpdatedBy = User.Identity?.Name;

                    _context.PrepaymentLineItems.RemoveRange(prepayment.LineItems);
                    prepayment.LineItems.Clear();

                    await _context.SaveChangesAsync();
                    await RecreatePrepaymentJournalEntryAsync(prepayment);
                    await dirTx.CommitAsync();

                    TempData["Success"] = $"Prepayment {prepayment.PrepaymentNumber} updated. Direct deposit ₦{prepayment.Amount:N2}.";
                    return RedirectToAction(nameof(Details), new { id = prepayment.Id });
                }
                catch (Exception ex)
                {
                    await dirTx.RollbackAsync();
                    _logger.LogError(ex, "Error updating direct-amount prepayment {PrepaymentId}. Transaction rolled back.", id);
                    ModelState.AddModelError(string.Empty, "An error occurred while updating the prepayment. Please try again.");
                    await PopulateDropdownsAsync(model);
                    return View(model);
                }
            }

            // Line-items path (existing behaviour).
            var validLines = rawValidLines;

            // Re-compute VAT / rebate breakdown for the current customer settings.
            // If settings changed since creation, Edit uses the new ones (matches
            // what the operator sees on screen). UnitPrice remains the raw customer
            // catalog price posted from the form; VatAmount / RebateAmount are derived.
            await ComputeLineBreakdownAsync(model.CustomerId.Value, validLines);

            // Line total model (new): qty × UnitPrice + VatAmount − RebateAmount.
            foreach (var li in validLines)
            {
                li.LineTotal = Math.Round(li.Quantity * li.UnitPrice + li.VatAmount - li.RebateAmount, 2);
            }
            model.TotalAmount = validLines.Sum(li => li.LineTotal);

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                prepayment.CustomerId = model.CustomerId.Value;
                prepayment.PrepaymentDate = model.PrepaymentDate;
                prepayment.ExpectedPickupDate = model.ExpectedPickupDate;
                prepayment.PaymentMethod = await ResolvePaymentMethodNameAsync(model.PaymentMethodId, model.PaymentMethod);
                prepayment.PaymentMethodId = model.PaymentMethodId;
                prepayment.Reference = model.Reference;
                prepayment.Notes = model.Notes;
                prepayment.Status = string.IsNullOrWhiteSpace(model.Status) ? prepayment.Status : model.Status;
                prepayment.Amount = model.TotalAmount;
                prepayment.UpdatedAt = DateTime.Now;
                prepayment.UpdatedBy = User.Identity?.Name;

                // Simplest correct approach: replace all line items. Safe because
                // we've already refused editing if any applications exist.
                _context.PrepaymentLineItems.RemoveRange(prepayment.LineItems);
                prepayment.LineItems.Clear();

                foreach (var li in validLines)
                {
                    prepayment.LineItems.Add(new PrepaymentLineItem
                    {
                        MaterialId = li.MaterialId!.Value,
                        Quantity = li.Quantity,
                        Unit = string.IsNullOrWhiteSpace(li.Unit) ? "Ton" : li.Unit,
                        UnitPrice = li.UnitPrice,
                        VatAmount = li.VatAmount,
                        RebateAmount = li.RebateAmount,
                        LineTotal = li.LineTotal,
                        UsedQuantity = 0m,
                        UsedAmount = 0m,
                        CreatedAt = DateTime.Now
                    });
                }

                await _context.SaveChangesAsync();

                // Reverse any prior ADV journal entry and re-post with the new amount,
                // using the same "delete-and-repost" approach used by OpeningBalance.
                await RecreatePrepaymentJournalEntryAsync(prepayment);

                await transaction.CommitAsync();

                TempData["Success"] = $"Prepayment {prepayment.PrepaymentNumber} updated successfully.";
                _logger.LogInformation("Prepayment {PrepaymentNumber} updated by {UserName}",
                    prepayment.PrepaymentNumber, User.Identity?.Name);

                return RedirectToAction(nameof(Details), new { id = prepayment.Id });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error updating prepayment {PrepaymentId}. The transaction was rolled back.", id);
                ModelState.AddModelError(string.Empty, "An error occurred while updating the prepayment. Please try again.");
                await PopulateDropdownsAsync(model);
                return View(model);
            }
        }

        // ----------------------------------------------------------------------
        // GET: Prepayment/Details/5
        // ----------------------------------------------------------------------
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var prepayment = await _context.CustomerPrepayments
                .Include(p => p.Customer)
                .Include(p => p.LineItems)
                    .ThenInclude(li => li.Material)
                .Include(p => p.Applications)
                    .ThenInclude(a => a.Invoice)
                .Include(p => p.Applications)
                    .ThenInclude(a => a.PrepaymentLineItem)
                        .ThenInclude(li => li!.Material)
                .FirstOrDefaultAsync(p => p.Id == id.Value);

            if (prepayment == null) return NotFound();

            var totalApplied = prepayment.Applications?.Sum(a => a.AppliedAmount) ?? 0m;
            var vm = new PrepaymentDetailsViewModel
            {
                Prepayment = prepayment,
                LineItems = prepayment.LineItems.OrderBy(li => li.Id).ToList(),
                Applications = prepayment.Applications?.OrderBy(a => a.AppliedDate).ThenBy(a => a.Id).ToList() ?? new(),
                TotalApplied = totalApplied,
                TotalRemaining = prepayment.Amount - totalApplied
            };

            return View(vm);
        }

        // ----------------------------------------------------------------------
        // GET: Prepayment/Receipt/5 — printable receipt
        // ----------------------------------------------------------------------
        public async Task<IActionResult> Receipt(int? id)
        {
            if (id == null) return NotFound();

            var prepayment = await _context.CustomerPrepayments
                .Include(p => p.Customer)
                .Include(p => p.LineItems)
                    .ThenInclude(li => li.Material)
                .FirstOrDefaultAsync(p => p.Id == id.Value);

            if (prepayment == null) return NotFound();

            var vm = new PrepaymentReceiptPrintViewModel
            {
                Prepayment = prepayment,
                LineItems = prepayment.LineItems.OrderBy(li => li.Id).ToList(),
                AmountInWords = NumberToWordsConverter.ConvertToWords(prepayment.Amount),
                CompanyDetails = GetCompanyDetails(),
                PrintDate = DateTime.Now
            };

            return View(vm);
        }

        // ----------------------------------------------------------------------
        // GET: Prepayment/Delete/5
        // ----------------------------------------------------------------------
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var prepayment = await _context.CustomerPrepayments
                .Include(p => p.Customer)
                .Include(p => p.LineItems)
                    .ThenInclude(li => li.Material)
                .Include(p => p.Applications)
                .FirstOrDefaultAsync(p => p.Id == id.Value);

            if (prepayment == null) return NotFound();

            return View(prepayment);
        }

        // ----------------------------------------------------------------------
        // POST: Prepayment/Delete/5
        // ----------------------------------------------------------------------
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var prepayment = await _context.CustomerPrepayments
                .Include(p => p.Applications)
                .Include(p => p.LineItems)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (prepayment == null) return NotFound();

            if (prepayment.Applications != null && prepayment.Applications.Any())
            {
                TempData["Error"] = "Cannot delete a prepayment that has already been applied to invoices.";
                return RedirectToAction(nameof(Index));
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Reverse the ADV journal entry too
                var advEntryPrefix = $"ADV/{prepayment.PrepaymentNumber}";
                var priorEntries = await _context.JournalEntries
                    .Include(je => je.JournalEntryLines)
                    .Where(je => je.Reference != null && je.Reference.Contains(prepayment.PrepaymentNumber) && je.EntryNumber.StartsWith("ADV"))
                    .ToListAsync();

                foreach (var prior in priorEntries)
                {
                    _context.JournalEntryLines.RemoveRange(prior.JournalEntryLines);
                }
                _context.JournalEntries.RemoveRange(priorEntries);

                _context.PrepaymentLineItems.RemoveRange(prepayment.LineItems);
                _context.CustomerPrepayments.Remove(prepayment);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                TempData["Success"] = $"Prepayment {prepayment.PrepaymentNumber} deleted successfully.";
                _logger.LogInformation("Prepayment {PrepaymentNumber} deleted by {UserName}",
                    prepayment.PrepaymentNumber, User.Identity?.Name);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error deleting prepayment {PrepaymentId}", id);
                TempData["Error"] = "An error occurred while deleting the prepayment.";
            }

            return RedirectToAction(nameof(Index));
        }

        // ----------------------------------------------------------------------
        // AJAX: Get material info for line-item auto-populate.
        // When a customer is selected, prefer their custom price (pricing service
        // honors per-customer history). Without a customer, fall back to the
        // material's catalog price.
        //
        // Also returns the customer's VAT type (for gross-up display) and any
        // per-unit rebate so the client can show the discounted line total
        // immediately as the user types.
        // ----------------------------------------------------------------------
        [HttpGet]
        public async Task<JsonResult> GetMaterialInfo(int materialId, int? customerId = null)
        {
            if (customerId.HasValue && customerId.Value > 0)
            {
                var pricing = await _pricingService.GetPricingAsync(customerId.Value, materialId);

                // Still want the unit label (Ton/kg) from the material catalog.
                var unit = await _context.Materials
                    .Where(m => m.Id == materialId)
                    .Select(m => m.Unit)
                    .FirstOrDefaultAsync() ?? "Ton";

                // Per-unit rebate for prepayments. The Customer.RebateAmount is the
                // per-unit rebate (rebate scales with quantity on prepayments),
                // which differs from how it's applied on invoices (flat discount).
                var customerRebate = await _context.Customers
                    .Where(c => c.Id == customerId.Value)
                    .Select(c => new { c.HasRebate, c.RebateAmount })
                    .FirstOrDefaultAsync();

                return Json(new
                {
                    success = true,
                    unitPrice = pricing.UnitPrice,
                    vatRate = pricing.VatRate,
                    unit,
                    isCustomerSpecific = pricing.IsCustomerSpecific,
                    vatType = pricing.VatType,
                    hasRebate = customerRebate?.HasRebate ?? false,
                    rebateAmount = customerRebate?.RebateAmount ?? 0m
                });
            }

            var material = await _context.Materials
                .FirstOrDefaultAsync(m => m.Id == materialId);

            if (material == null)
            {
                return Json(new { success = false, message = "Material not found." });
            }

            return Json(new
            {
                success = true,
                unitPrice = material.UnitPrice,
                vatRate = material.VatRate,
                unit = material.Unit,
                isCustomerSpecific = false,
                vatType = (string?)null,
                hasRebate = false,
                rebateAmount = 0m
            });
        }

        // ----------------------------------------------------------------------
        // Helpers
        // ----------------------------------------------------------------------

        /// <summary>
        /// For each line, compute the VAT and rebate amounts that correspond to
        /// the raw UnitPrice (the customer's catalog price) and the customer's
        /// current VAT type + rebate settings. The breakdown is stored alongside
        /// UnitPrice so the UI can render three separate columns (Unit Price /
        /// VAT / Rebate) instead of baking everything into a single number.
        /// <para/>
        /// Math (per line):
        ///   Exclusive: perUnitVat = UnitPrice × rate / 100
        ///   Inclusive: perUnitVat = UnitPrice × rate / (100 + rate)
        ///   perUnitRebate = customer.RebateAmount (clamped to UnitPrice + perUnitVat)
        ///   VatAmount    = round(perUnitVat * qty, 2)
        ///   RebateAmount = round(perUnitRebate * qty, 2)
        /// <para/>
        /// The caller is responsible for LineTotal. The expected formula is:
        ///   LineTotal = qty × UnitPrice + VatAmount − RebateAmount
        /// </summary>
        private async Task ComputeLineBreakdownAsync(int customerId, List<PrepaymentLineItemInput> lines)
        {
            if (lines == null || lines.Count == 0) return;

            var customer = await _context.Customers
                .Include(c => c.VatType)
                .FirstOrDefaultAsync(c => c.Id == customerId);

            var vatType = customer?.VatType?.Name ?? "Exclusive";
            var perUnitRebate = (customer != null && customer.HasRebate) ? (customer.RebateAmount ?? 0m) : 0m;

            foreach (var li in lines)
            {
                if (!li.MaterialId.HasValue)
                {
                    li.VatAmount = 0m;
                    li.RebateAmount = 0m;
                    continue;
                }

                // Resolve VAT rate for this customer+material via the pricing service
                // (returns the material default when the customer has no override).
                var pricing = await _pricingService.GetPricingAsync(customerId, li.MaterialId.Value);
                var vatRate = pricing.VatRate;

                // Per-unit VAT computed from the raw UnitPrice.
                decimal unitVat;
                if (vatRate <= 0)
                {
                    unitVat = 0m;
                }
                else if (string.Equals(vatType, "Inclusive", StringComparison.OrdinalIgnoreCase))
                {
                    // Inclusive: the UnitPrice already includes VAT, so the VAT share is
                    // embedded in it. Back it out proportionally.
                    unitVat = li.UnitPrice * vatRate / (100m + vatRate);
                }
                else
                {
                    // Exclusive: VAT is added on top of the raw price.
                    unitVat = li.UnitPrice * vatRate / 100m;
                }

                // Per-unit rebate clamped so a line can't go negative.
                var cappedRebate = Math.Min(perUnitRebate, li.UnitPrice + unitVat);

                li.VatAmount = Math.Round(unitVat * li.Quantity, 2);
                li.RebateAmount = Math.Round(cappedRebate * li.Quantity, 2);
            }
        }

        private async Task PopulateDropdownsAsync(PrepaymentCreateEditViewModel model)
        {
            model.Customers = await _context.Customers
                .Where(c => c.Status == "Active")
                .OrderBy(c => c.Name)
                .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name })
                .ToListAsync();

            model.Materials = await _context.Materials
                .Where(m => m.Status == "Active")
                .OrderBy(m => m.Name)
                .Select(m => new SelectListItem { Value = m.Id.ToString(), Text = $"{m.Name} ({m.Type})" })
                .ToListAsync();

            // Payment methods from the lookup table. Only active ones show up
            // in the dropdown; inactive methods stay in the DB so historical
            // FKs don't break. DisplayOrder controls ranking so Cash lands at
            // the top; ties are broken by Id for stability.
            model.PaymentMethods = await _context.PaymentMethods
                .Where(pm => pm.IsActive)
                .OrderBy(pm => pm.DisplayOrder)
                .ThenBy(pm => pm.Id)
                .Select(pm => new SelectListItem
                {
                    Value = pm.Id.ToString(),
                    Text = pm.Name
                })
                .ToListAsync();
        }

        /// <summary>
        /// Resolves the canonical payment method name from the selected FK. Used
        /// to denormalize into the legacy <see cref="CustomerPrepayment.PaymentMethod"/>
        /// string so old reports and string-based queries keep working.
        /// <para/>
        /// When <paramref name="paymentMethodId"/> is null (no selection made), we
        /// fall back to whatever was posted in the legacy string field — this
        /// happens only on records that predate the lookup table. Returns an
        /// empty string if neither source has a value, matching the old behavior.
        /// </summary>
        private async Task<string> ResolvePaymentMethodNameAsync(int? paymentMethodId, string? fallback)
        {
            if (paymentMethodId.HasValue && paymentMethodId.Value > 0)
            {
                var name = await _context.PaymentMethods
                    .Where(pm => pm.Id == paymentMethodId.Value)
                    .Select(pm => pm.Name)
                    .FirstOrDefaultAsync();
                if (!string.IsNullOrWhiteSpace(name))
                {
                    return name;
                }
            }
            return fallback ?? string.Empty;
        }

        private PrepaymentCreateEditViewModel MapToEditViewModel(CustomerPrepayment prepayment)
        {
            // Direct-amount detection: a prepayment with no line items but a
            // non-zero Amount was created via the "Direct Amount" path. Surface
            // that value back into the form's DirectAmount so Edit shows it
            // exactly as it was entered.
            decimal? directAmount = (prepayment.LineItems == null || prepayment.LineItems.Count == 0)
                ? (prepayment.Amount > 0m ? (decimal?)prepayment.Amount : null)
                : null;

            return new PrepaymentCreateEditViewModel
            {
                Id = prepayment.Id,
                PrepaymentNumber = prepayment.PrepaymentNumber,
                CustomerId = prepayment.CustomerId,
                PrepaymentDate = prepayment.PrepaymentDate,
                ExpectedPickupDate = prepayment.ExpectedPickupDate,
                PaymentMethod = prepayment.PaymentMethod ?? string.Empty,
                PaymentMethodId = prepayment.PaymentMethodId,
                Reference = prepayment.Reference,
                Notes = prepayment.Notes,
                Status = prepayment.Status,
                TotalAmount = prepayment.Amount,
                DirectAmount = directAmount,
                Items = prepayment.LineItems
                    .OrderBy(li => li.Id)
                    .Select(li => new PrepaymentLineItemInput
                    {
                        Id = li.Id,
                        MaterialId = li.MaterialId,
                        Quantity = li.Quantity,
                        Unit = li.Unit,
                        UnitPrice = li.UnitPrice,
                        VatAmount = li.VatAmount,
                        RebateAmount = li.RebateAmount,
                        LineTotal = li.LineTotal,
                        UsedAmount = li.UsedAmount,
                        UsedQuantity = li.UsedQuantity
                    })
                    .ToList()
            };
        }

        private async Task<string> GeneratePrepaymentNumberAsync()
        {
            var today = DateTime.Today;
            var prefix = $"ADV/NG/{today:yyyy}/";

            var last = await _context.CustomerPrepayments
                .Where(p => p.PrepaymentNumber.StartsWith(prefix))
                .OrderByDescending(p => p.PrepaymentNumber)
                .FirstOrDefaultAsync();

            int nextNumber = 1;
            if (last != null)
            {
                var suffix = last.PrepaymentNumber.Substring(prefix.Length);
                if (int.TryParse(suffix, out var lastNumber))
                {
                    nextNumber = lastNumber + 1;
                }
            }

            return $"{prefix}{nextNumber:D4}";
        }

        /// <summary>
        /// Posts the Dr Cash / Cr Customer Prepayment liability journal entry for a
        /// newly created prepayment, ensuring a per-customer 2103-xxxxxx sub-account exists.
        /// </summary>
        private async Task CreatePrepaymentJournalEntryAsync(CustomerPrepayment prepayment)
        {
            var cashAccountId = await GetCashAccountId();
            var customerPrepaymentAccountId = await EnsureCustomerPrepaymentLedgerAccountAsync(prepayment);

            var entryNumber = JournalEntry.GenerateEntryNumber("ADV");
            var postedByUserId = _userManager.GetUserId(User);

            var journalEntry = new JournalEntry
            {
                EntryNumber = entryNumber,
                EntryDate = prepayment.PrepaymentDate,
                Reference = $"Prepayment {prepayment.PrepaymentNumber}",
                Description = $"Customer prepayment of {prepayment.Amount:C} for customer {prepayment.CustomerId}",
                PostedBy = postedByUserId,
                IsAutoGenerated = true,
                CreatedAt = DateTime.Now
            };

            journalEntry.JournalEntryLines.Add(new JournalEntryLine
            {
                AccountId = cashAccountId,
                DebitAmount = prepayment.Amount,
                CreditAmount = 0,
                LineDescription = $"Prepayment received - {prepayment.PrepaymentNumber}"
            });

            journalEntry.JournalEntryLines.Add(new JournalEntryLine
            {
                AccountId = customerPrepaymentAccountId,
                DebitAmount = 0,
                CreditAmount = prepayment.Amount,
                LineDescription = $"Customer prepayment liability - {prepayment.PrepaymentNumber}"
            });

            journalEntry.RecalculateTotals();

            _context.JournalEntries.Add(journalEntry);
            await _context.SaveChangesAsync();

            await RecalculateAccountBalanceAsync(cashAccountId);
            await RecalculateAccountBalanceAsync(customerPrepaymentAccountId);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// For the Edit flow: removes any prior ADV journal entry tagged with this
        /// prepayment number and re-posts a fresh one at the updated amount.
        /// </summary>
        private async Task RecreatePrepaymentJournalEntryAsync(CustomerPrepayment prepayment)
        {
            var priorEntries = await _context.JournalEntries
                .Include(je => je.JournalEntryLines)
                .Where(je =>
                    je.EntryNumber.StartsWith("ADV") &&
                    je.Reference != null &&
                    je.Reference.Contains(prepayment.PrepaymentNumber))
                .ToListAsync();

            foreach (var prior in priorEntries)
            {
                _context.JournalEntryLines.RemoveRange(prior.JournalEntryLines);
            }
            _context.JournalEntries.RemoveRange(priorEntries);
            await _context.SaveChangesAsync();

            await CreatePrepaymentJournalEntryAsync(prepayment);
        }

        private async Task RecalculateAccountBalanceAsync(int accountId)
        {
            var account = await _context.ChartOfAccounts.FirstOrDefaultAsync(a => a.Id == accountId);
            if (account == null) return;

            var totals = await _context.JournalEntryLines
                .Where(l => l.AccountId == accountId)
                .GroupBy(l => l.AccountId)
                .Select(g => new { Debit = g.Sum(l => l.DebitAmount), Credit = g.Sum(l => l.CreditAmount) })
                .FirstOrDefaultAsync();

            decimal totalDebit = totals?.Debit ?? 0;
            decimal totalCredit = totals?.Credit ?? 0;
            decimal netMovement = (account.IsAssetAccount() || account.IsExpenseAccount())
                ? totalDebit - totalCredit
                : totalCredit - totalDebit;
            account.CurrentBalance = account.OpeningBalance + netMovement;
        }

        private async Task<int> GetCashAccountId()
        {
            var cashAccount = await _context.ChartOfAccounts.FirstOrDefaultAsync(ca => ca.AccountCode == "1001");
            return cashAccount?.Id ?? 1;
        }

        private string GenerateCustomerPrepaymentAccountCode(int customerId) => $"2103-{customerId:D6}";

        private async Task<int> EnsureCustomerPrepaymentLedgerAccountAsync(CustomerPrepayment prepayment)
        {
            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Id == prepayment.CustomerId);
            if (customer == null)
            {
                throw new InvalidOperationException($"Customer with Id {prepayment.CustomerId} not found when creating prepayment ledger account.");
            }

            var accountCode = GenerateCustomerPrepaymentAccountCode(customer.Id);
            var desiredName = $"Customer Prepayments - {customer.Name}";
            var desiredActive = customer.Status == "Active";

            var existing = await _context.ChartOfAccounts.FirstOrDefaultAsync(a => a.AccountCode == accountCode);
            if (existing != null)
            {
                if (!string.Equals(existing.AccountName, desiredName, StringComparison.Ordinal) ||
                    existing.IsActive != desiredActive)
                {
                    existing.AccountName = desiredName;
                    existing.IsActive = desiredActive;
                    await _context.SaveChangesAsync();
                }
                return existing.Id;
            }

            var account = new ChartOfAccounts
            {
                AccountCode = accountCode,
                AccountName = desiredName,
                AccountType = "Liability",
                SubType = "Current",
                OpeningBalance = 0m,
                CurrentBalance = 0m,
                IsActive = desiredActive,
                CreatedAt = DateTime.Now
            };
            _context.ChartOfAccounts.Add(account);
            await _context.SaveChangesAsync();
            return account.Id;
        }

        private CompanyDetailsViewModel GetCompanyDetails()
        {
            return new CompanyDetailsViewModel
            {
                CompanyName = "Nigerian Quarry Management System",
                Address = "123 Quarry Road, Industrial Estate",
                City = "Lagos",
                State = "Lagos",
                Phone = "+234-1-2345678",
                Email = "info@quarry.ng",
                Website = "www.quarry.ng",
                TaxNumber = "12345678-0001",
                BankDetails = "Access Bank, Account: 1234567890"
            };
        }
    }
}
