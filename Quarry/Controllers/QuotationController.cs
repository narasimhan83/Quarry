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
    public class QuotationController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<QuotationController> _logger;

        public QuotationController(ApplicationDbContext context, ILogger<QuotationController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: Quotation
        public async Task<IActionResult> Index(string searchTerm, string status, DateTime? dateFrom, DateTime? dateTo, int page = 1)
        {
            try
            {
                int pageSize = 20;
                var query = _context.Quotations
                    .Include(q => q.Customer)
                    .AsQueryable();

                if (!string.IsNullOrEmpty(searchTerm))
                {
                    query = query.Where(q =>
                        q.QuotationNumber.Contains(searchTerm) ||
                        q.Customer.Name.Contains(searchTerm));
                }

                if (!string.IsNullOrEmpty(status))
                {
                    query = query.Where(q => q.Status == status);
                }

                if (dateFrom.HasValue)
                {
                    query = query.Where(q => q.QuotationDate >= dateFrom.Value);
                }

                if (dateTo.HasValue)
                {
                    query = query.Where(q => q.QuotationDate <= dateTo.Value.AddDays(1));
                }

                var totalCount = await query.CountAsync();
                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

                var quotations = await query
                    .OrderByDescending(q => q.QuotationDate)
                    .ThenByDescending(q => q.Id)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var viewModel = new QuotationListViewModel
                {
                    Quotations = quotations,
                    SearchTerm = searchTerm,
                    SelectedStatus = status,
                    DateFrom = dateFrom,
                    DateTo = dateTo,
                    CurrentPage = page,
                    TotalPages = totalPages,
                    TotalCount = totalCount,
                    Statuses = GetQuotationStatuses()
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading quotation list");
                return View(new QuotationListViewModel
                {
                    ErrorMessage = "An error occurred while loading quotations. Please try again."
                });
            }
        }

        // GET: Quotation/Create
        public async Task<IActionResult> Create()
        {
            try
            {
                var viewModel = new QuotationCreateEditViewModel
                {
                    QuotationDate = DateTime.Now,
                    ExpiryDate = DateTime.Now.AddDays(30),
                    Items = new List<QuotationItemEditViewModel> { new QuotationItemEditViewModel() }
                };

                await PopulateDropdowns(viewModel);
                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading create quotation form");
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Quotation/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(QuotationCreateEditViewModel model)
        {
            try
            {
                if (!model.CustomerId.HasValue)
                {
                    ModelState.AddModelError("CustomerId", "Please select a customer.");
                }

                if (model.Items == null || !model.Items.Any(i => i.Quantity > 0 && (i.UnitPrice > 0 || i.MaterialId.HasValue)))
                {
                    ModelState.AddModelError("", "Please add at least one valid item.");
                }

                if (ModelState.IsValid)
                {
                    decimal subTotal = 0, vatAmount = 0, totalAmount = 0;
                    foreach (var it in model.Items)
                    {
                        var lineSub = Math.Round(it.Quantity * it.UnitPrice, 2);
                        var lineVat = Math.Round(lineSub * (it.VatRate / 100m), 2);
                        subTotal += lineSub;
                        vatAmount += lineVat;
                        totalAmount += lineSub + lineVat;
                    }

                    var quotationNumber = await GenerateQuotationNumber();

                    var quotation = new Quotation
                    {
                        QuotationNumber = quotationNumber,
                        CustomerId = model.CustomerId.Value,
                        QuotationDate = model.QuotationDate,
                        ExpiryDate = model.ExpiryDate,
                        SubTotal = subTotal,
                        VatAmount = vatAmount,
                        TotalAmount = totalAmount,
                        Status = model.Status ?? "Draft",
                        Notes = model.Notes,
                        CreatedBy = User.Identity?.Name,
                        CreatedAt = DateTime.Now
                    };

                    // Items
                    foreach (var it in model.Items)
                    {
                        if (it.Quantity <= 0 && it.UnitPrice <= 0 && !it.MaterialId.HasValue) continue;
                        var qi = new QuotationItem
                        {
                            MaterialId = it.MaterialId,
                            Description = it.Description,
                            Quantity = it.Quantity,
                            Unit = it.Unit,
                            UnitPrice = it.UnitPrice,
                            VatRate = it.VatRate
                        };
                        qi.Recalculate();
                        quotation.Items.Add(qi);
                    }

                    _context.Quotations.Add(quotation);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = $"Quotation {quotationNumber} created successfully.";
                    _logger.LogInformation("Quotation {QuotationNumber} created by user {UserName} for customer {CustomerId}", quotationNumber, User.Identity?.Name, model.CustomerId);
                    return RedirectToAction(nameof(Index));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating quotation");
                ModelState.AddModelError("", "An error occurred while creating the quotation. Please try again.");
            }

            await PopulateDropdowns(model);
            return View(model);
        }

        // GET: Quotation/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var quotation = await _context.Quotations
                .Include(q => q.Items)
                .Include(q => q.Customer)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (quotation == null)
            {
                return NotFound();
            }

            var viewModel = new QuotationCreateEditViewModel
            {
                Id = quotation.Id,
                QuotationDate = quotation.QuotationDate,
                ExpiryDate = quotation.ExpiryDate,
                CustomerId = quotation.CustomerId,
                Notes = quotation.Notes,
                Status = quotation.Status,
                Items = quotation.Items.Select(i => new QuotationItemEditViewModel
                {
                    Id = i.Id,
                    MaterialId = i.MaterialId,
                    Description = i.Description,
                    Quantity = i.Quantity,
                    Unit = i.Unit,
                    UnitPrice = i.UnitPrice,
                    VatRate = i.VatRate
                }).ToList()
            };

            await PopulateDropdowns(viewModel);
            return View(viewModel);
        }

        // POST: Quotation/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, QuotationCreateEditViewModel model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            try
            {
                if (!model.CustomerId.HasValue)
                {
                    ModelState.AddModelError("CustomerId", "Please select a customer.");
                }

                if (model.Items == null || !model.Items.Any(i => i.Quantity > 0 && (i.UnitPrice > 0 || i.MaterialId.HasValue)))
                {
                    ModelState.AddModelError("", "Please add at least one valid item.");
                }

                if (ModelState.IsValid)
                {
                    var quotation = await _context.Quotations
                        .Include(q => q.Items)
                        .FirstOrDefaultAsync(q => q.Id == id);

                    if (quotation == null)
                    {
                        return NotFound();
                    }

                    quotation.QuotationDate = model.QuotationDate;
                    quotation.ExpiryDate = model.ExpiryDate;
                    quotation.CustomerId = model.CustomerId.Value;
                    quotation.Notes = model.Notes;
                    quotation.Status = model.Status ?? quotation.Status;
                    quotation.UpdatedAt = DateTime.Now;

                    // Rebuild items
                    _context.QuotationItems.RemoveRange(quotation.Items);
                    quotation.Items.Clear();

                    decimal subTotal = 0, vatAmount = 0, totalAmount = 0;
                    foreach (var it in model.Items)
                    {
                        if (it.Quantity <= 0 && it.UnitPrice <= 0 && !it.MaterialId.HasValue) continue;
                        var qi = new QuotationItem
                        {
                            MaterialId = it.MaterialId,
                            Description = it.Description,
                            Quantity = it.Quantity,
                            Unit = it.Unit,
                            UnitPrice = it.UnitPrice,
                            VatRate = it.VatRate
                        };
                        qi.Recalculate();
                        subTotal += qi.LineSubTotal;
                        vatAmount += qi.LineVatAmount;
                        totalAmount += qi.LineTotal;
                        quotation.Items.Add(qi);
                    }

                    quotation.SubTotal = subTotal;
                    quotation.VatAmount = vatAmount;
                    quotation.TotalAmount = totalAmount;

                    await _context.SaveChangesAsync();

                    TempData["Success"] = $"Quotation {quotation.QuotationNumber} updated successfully.";
                    return RedirectToAction(nameof(Details), new { id = quotation.Id });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating quotation {QuotationId}", id);
                ModelState.AddModelError("", "An error occurred while updating the quotation.");
            }

            await PopulateDropdowns(model);
            return View(model);
        }

        // GET: Quotation/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var quotation = await _context.Quotations
                .Include(q => q.Customer)
                .Include(q => q.Items).ThenInclude(i => i.Material)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (quotation == null)
            {
                return NotFound();
            }

            var viewModel = new QuotationDetailsViewModel
            {
                Quotation = quotation,
                Items = quotation.Items.OrderBy(i => i.Id).ToList(),
                AmountInWords = quotation.GetAmountInWords()
            };

            return View(viewModel);
        }

        // GET: Quotation/Print/5
        public async Task<IActionResult> Print(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var quotation = await _context.Quotations
                .Include(q => q.Customer)
                .Include(q => q.Items).ThenInclude(i => i.Material)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (quotation == null)
            {
                return NotFound();
            }

            var viewModel = new QuotationPrintViewModel
            {
                Quotation = quotation,
                Items = quotation.Items.ToList(),
                AmountInWords = quotation.GetAmountInWords(),
                CompanyDetails = GetCompanyDetails()
            };

            return View(viewModel);
        }

        // GET: Quotation/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var quotation = await _context.Quotations
                .Include(q => q.Customer)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (quotation == null)
            {
                return NotFound();
            }

            return View(quotation);
        }

        // POST: Quotation/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var quotation = await _context.Quotations
                    .Include(q => q.Items)
                    .FirstOrDefaultAsync(q => q.Id == id);

                if (quotation == null)
                {
                    return NotFound();
                }

                _context.QuotationItems.RemoveRange(quotation.Items);
                _context.Quotations.Remove(quotation);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Quotation deleted successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting quotation {QuotationId}", id);
                TempData["Error"] = "An error occurred while deleting the quotation.";
            }

            return RedirectToAction(nameof(Index));
        }

        // Helpers
        private async Task PopulateDropdowns(QuotationCreateEditViewModel model)
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
                .OrderBy(m => m.Name)
                .Select(m => new SelectListItem
                {
                    Value = m.Id.ToString(),
                    Text = $"{m.Name} (₦{m.UnitPrice:N2}/{m.Unit})"
                })
                .ToListAsync();
        }

        private List<SelectListItem> GetQuotationStatuses()
        {
            return new List<SelectListItem>
            {
                new SelectListItem { Value = "", Text = "-- All Statuses --" },
                new SelectListItem { Value = "Draft", Text = "Draft" },
                new SelectListItem { Value = "Sent", Text = "Sent" },
                new SelectListItem { Value = "Accepted", Text = "Accepted" },
                new SelectListItem { Value = "Rejected", Text = "Rejected" },
                new SelectListItem { Value = "Cancelled", Text = "Cancelled" },
                new SelectListItem { Value = "Expired", Text = "Expired" }
            };
        }

        private async Task<string> GenerateQuotationNumber()
        {
            var today = DateTime.Today;
            var prefix = $"QTN/NG/{today:yyyy}/";

            var lastQuotation = await _context.Quotations
                .Where(q => q.QuotationNumber.StartsWith(prefix))
                .OrderByDescending(q => q.QuotationNumber)
                .FirstOrDefaultAsync();

            int nextNumber = 1;
            if (lastQuotation != null)
            {
                var lastNumberStr = lastQuotation.QuotationNumber.Substring(prefix.Length);
                if (int.TryParse(lastNumberStr, out int lastNumber))
                {
                    nextNumber = lastNumber + 1;
                }
            }

            return $"{prefix}{nextNumber:D4}";
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