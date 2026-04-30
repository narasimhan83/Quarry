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
    [Authorize(Roles = "Admin,Manager,Accountant")]
    public class CustomerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CustomerController> _logger;
        private readonly ICustomerPricingService _pricingService;

        public CustomerController(
            ApplicationDbContext context,
            ILogger<CustomerController> logger,
            ICustomerPricingService pricingService)
        {
            _context = context;
            _logger = logger;
            _pricingService = pricingService;
        }

        // GET: Customer
        public async Task<IActionResult> Index(string searchTerm, string state, string status, int page = 1)
        {
            try
            {
                int pageSize = 20;
                var query = _context.Customers
                    .Include(c => c.CustomerType)
                    .Include(c => c.VatType)
                    .AsQueryable();

                if (!string.IsNullOrEmpty(searchTerm))
                {
                    query = query.Where(c =>
                        c.Name.Contains(searchTerm) ||
                        (c.ContactPerson != null && c.ContactPerson.Contains(searchTerm)) ||
                        (c.Phone != null && c.Phone.Contains(searchTerm)) ||
                        (c.Email != null && c.Email.Contains(searchTerm)));
                }
                if (!string.IsNullOrEmpty(state))
                {
                    query = query.Where(c => c.State == state);
                }
                if (!string.IsNullOrEmpty(status))
                {
                    query = query.Where(c => c.Status == status);
                }

                var totalCount = await query.CountAsync();
                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

                var customers = await query
                    .OrderByDescending(c => c.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var viewModel = new CustomerListViewModel
                {
                    Customers = customers,
                    SearchTerm = searchTerm,
                    SelectedState = state,
                    SelectedStatus = status,
                    CurrentPage = page,
                    TotalPages = totalPages,
                    TotalCount = totalCount,
                    States = GetNigerianStates(),
                    Statuses = GetCustomerStatuses()
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading customer list");
                return View(new CustomerListViewModel
                {
                    ErrorMessage = "An error occurred while loading customers. Please try again."
                });
            }
        }

        // GET: Customer/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var customer = await _context.Customers
                .Include(c => c.CustomerType)
                .Include(c => c.VatType)
                .Include(c => c.WeighmentTransactions).ThenInclude(w => w.Material)
                .Include(c => c.Invoices)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (customer == null) return NotFound();

            // Current prices (one per material — the most recent effective row).
            var currentPricesRaw = await _context.CustomerMaterialPrices
                .Include(cmp => cmp.Material)
                .Where(cmp => cmp.CustomerId == customer.Id)
                .ToListAsync();

            var currentPrices = currentPricesRaw
                .GroupBy(cmp => cmp.MaterialId)
                .Select(g =>
                {
                    var latest = g.OrderByDescending(cmp => cmp.EffectiveFrom).ThenByDescending(cmp => cmp.Id).First();
                    return new CustomerMaterialPriceDisplay
                    {
                        MaterialId = latest.MaterialId,
                        MaterialName = latest.Material?.Name ?? $"#{latest.MaterialId}",
                        UnitPrice = latest.UnitPrice,
                        VatRate = latest.VatRate,
                        EffectiveFrom = latest.EffectiveFrom,
                        HistoryCount = g.Count()
                    };
                })
                .OrderBy(p => p.MaterialName)
                .ToList();

            var vm = new CustomerDetailsViewModel
            {
                Customer = customer,
                CurrentPrices = currentPrices,
                TotalTransactions = customer.WeighmentTransactions?.Count ?? 0,
                TotalInvoiceAmount = customer.Invoices?.Sum(i => i.TotalAmount) ?? 0m,
                LastTransactionDate = customer.WeighmentTransactions?
                    .OrderByDescending(w => w.TransactionDate)
                    .FirstOrDefault()?.TransactionDate
            };
            if (vm.TotalTransactions > 0)
            {
                vm.AverageTransactionValue = (customer.WeighmentTransactions?.Sum(w => w.TotalAmount ?? 0m) ?? 0m) / vm.TotalTransactions;
            }

            return View(vm);
        }

        // GET: Customer/Create
        public async Task<IActionResult> Create()
        {
            var model = new CustomerCreateViewModel
            {
                Status = "Active"
            };
            await PopulateDropdownsAsync(model);
            return View(model);
        }

        // POST: Customer/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CustomerCreateViewModel model)
        {
            // Drop empty truck rows BEFORE model validation runs. The form may
            // post blank rows (an "Add Truck" click that the operator didn't
            // fill in). Without this, [Required] on CustomerTruckNumber would
            // reject the whole submit. We also clear matching ModelState entries
            // so leftover validation errors don't survive.
            CleanEmptyTruckRows(model);
            CleanEmptyBankRows(model);

            // Conditional validation: if HasRebate is on, RebateAmount is required; same for transport.
            if (model.HasRebate && (!model.RebateAmount.HasValue || model.RebateAmount <= 0))
            {
                ModelState.AddModelError(nameof(model.RebateAmount), "Rebate amount is required when Rebate is enabled.");
            }
            if (model.TransportRequired && (!model.TransportAmount.HasValue || model.TransportAmount <= 0))
            {
                ModelState.AddModelError(nameof(model.TransportAmount), "Transport amount is required when Transport is enabled.");
            }

            if (!string.IsNullOrWhiteSpace(model.Phone) &&
                await _context.Customers.AnyAsync(c => c.Phone == model.Phone))
            {
                ModelState.AddModelError(nameof(model.Phone), "A customer with this phone number already exists.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateDropdownsAsync(model);
                return View(model);
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var customer = new Customer
                {
                    Name = model.Name.Trim(),
                    RCNumber = string.IsNullOrWhiteSpace(model.RCNumber)
                        ? await GenerateNextCustomerNumberAsync()
                        : model.RCNumber.Trim(),
                    Location = model.Location?.Trim() ?? string.Empty,
                    LGA = model.LGA?.Trim() ?? string.Empty,
                    State = model.State?.Trim() ?? string.Empty,
                    MiningLicenseNumber = model.MiningLicenseNumber?.Trim(),
                    ContactPerson = model.ContactPerson?.Trim(),
                    Phone = string.IsNullOrWhiteSpace(model.Phone) ? null : model.Phone.Trim(),
                    Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim(),
                    TIN = model.TIN?.Trim(),
                    BVN = model.BVN?.Trim(),
                    BillingAddress = model.BillingAddress?.Trim(),
                    CreditLimit = model.CreditLimit,
                    Status = string.IsNullOrWhiteSpace(model.Status) ? "Active" : model.Status,
                    CustomerTypeId = model.CustomerTypeId,
                    VatTypeId = model.VatTypeId,
                    HasRebate = model.HasRebate,
                    RebateAmount = model.HasRebate ? model.RebateAmount : null,
                    TransportRequired = model.TransportRequired,
                    TransportAmount = model.TransportRequired ? model.TransportAmount : null,
                    OutstandingBalance = 0,
                    CreatedAt = DateTime.Now
                };

                _context.Customers.Add(customer);
                await _context.SaveChangesAsync();

                // Save per-customer material pricing as history rows (IsCurrent = true).
                var validPrices = (model.MaterialPrices ?? new())
                    .Where(p => p.MaterialId.HasValue && p.MaterialId > 0 && p.UnitPrice > 0)
                    .ToList();

                foreach (var price in validPrices)
                {
                    await _pricingService.AddOrUpdatePriceAsync(
                        customer.Id,
                        price.MaterialId!.Value,
                        price.UnitPrice,
                        price.VatRate,
                        price.EffectiveFrom == default ? DateTime.Today : price.EffectiveFrom,
                        User.Identity?.Name,
                        price.Notes);
                }
                if (validPrices.Any())
                {
                    await _context.SaveChangesAsync();
                }

                // Save per-customer trucks (de-dup, trim, ignore blanks)
                var validTrucks = (model.Trucks ?? new())
                    .Where(t => !string.IsNullOrWhiteSpace(t.CustomerTruckNumber))
                    .GroupBy(t => t.CustomerTruckNumber.Trim(), StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .ToList();

                foreach (var truck in validTrucks)
                {
                    _context.CustomerTrucks.Add(new CustomerTruck
                    {
                        CustomerId = customer.Id,
                        CustomerTruckNumber = truck.CustomerTruckNumber.Trim(),
                        IsActive = truck.IsActive,
                        CreatedAt = DateTime.Now
                    });
                }
                if (validTrucks.Any())
                {
                    await _context.SaveChangesAsync();
                }

                // Save per-customer bank accounts (de-dup, trim, ignore blanks).
                // A row counts as blank when both account number AND bank name
                // are missing — either alone would have been caught earlier by
                // CleanEmptyBankRows / Required validators.
                var validBanks = (model.BankAccounts ?? new())
                    .Where(b => !string.IsNullOrWhiteSpace(b.AccountNumber) && !string.IsNullOrWhiteSpace(b.BankName))
                    .GroupBy(b => b.AccountNumber.Trim(), StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .ToList();

                foreach (var bank in validBanks)
                {
                    _context.CustomerBanks.Add(new CustomerBank
                    {
                        CustomerId = customer.Id,
                        AccountNumber = bank.AccountNumber.Trim(),
                        BankName = bank.BankName.Trim(),
                        BankAddress = bank.BankAddress?.Trim(),
                        BankBranch = bank.BankBranch?.Trim(),
                        BankSwiftCode = bank.BankSwiftCode?.Trim(),
                        IsActive = bank.IsActive,
                        CreatedAt = DateTime.Now
                    });
                }
                if (validBanks.Any())
                {
                    await _context.SaveChangesAsync();
                }

                await EnsureCustomerLedgerAccountAsync(customer);

                await transaction.CommitAsync();

                TempData["Success"] = $"Customer '{customer.Name}' created successfully"
                    + (validPrices.Any() ? $" with {validPrices.Count} custom price(s)." : ".");
                _logger.LogInformation("Customer {CustomerName} created by user {UserName}", customer.Name, User.Identity?.Name);

                return RedirectToAction(nameof(Details), new { id = customer.Id });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error creating customer");
                ModelState.AddModelError(string.Empty, "An error occurred while creating the customer. Please try again.");
                await PopulateDropdownsAsync(model);
                return View(model);
            }
        }

        // GET: Customer/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var customer = await _context.Customers
                .Include(c => c.MaterialPrices)
                    .ThenInclude(cmp => cmp.Material)
                .Include(c => c.Trucks)
                .Include(c => c.BankAccounts)
                .FirstOrDefaultAsync(c => c.Id == id.Value);

            if (customer == null) return NotFound();

            var model = new CustomerEditViewModel
            {
                Id = customer.Id,
                Name = customer.Name,
                RCNumber = customer.RCNumber,
                Location = customer.Location,
                LGA = customer.LGA,
                State = customer.State,
                MiningLicenseNumber = customer.MiningLicenseNumber,
                ContactPerson = customer.ContactPerson,
                Phone = customer.Phone,
                Email = customer.Email,
                TIN = customer.TIN,
                BVN = customer.BVN,
                BillingAddress = customer.BillingAddress,
                CreditLimit = customer.CreditLimit,
                OutstandingBalance = customer.OutstandingBalance,
                AvailableCredit = customer.AvailableCredit,
                Status = customer.Status ?? "Active",
                CustomerTypeId = customer.CustomerTypeId,
                VatTypeId = customer.VatTypeId,
                HasRebate = customer.HasRebate,
                RebateAmount = customer.RebateAmount,
                TransportRequired = customer.TransportRequired,
                TransportAmount = customer.TransportAmount
            };

            // Bring the latest effective price per material into the form so the
            // operator can review / update. New rows (Id = 0) are detected on submit.
            model.MaterialPrices = customer.MaterialPrices
                .GroupBy(cmp => cmp.MaterialId)
                .Select(g =>
                {
                    var latest = g.OrderByDescending(cmp => cmp.EffectiveFrom).ThenByDescending(cmp => cmp.Id).First();
                    return new CustomerMaterialPriceInput
                    {
                        Id = latest.Id,
                        MaterialId = latest.MaterialId,
                        UnitPrice = latest.UnitPrice,
                        VatRate = latest.VatRate,
                        EffectiveFrom = latest.EffectiveFrom,
                        Notes = latest.Notes
                    };
                })
                .OrderBy(p => p.MaterialId)
                .ToList();

            // Load existing trucks so the operator can edit / remove them.
            model.Trucks = customer.Trucks
                .OrderBy(t => t.CustomerTruckNumber)
                .Select(t => new CustomerTruckInput
                {
                    Id = t.CustomerTruckId,
                    CustomerTruckNumber = t.CustomerTruckNumber,
                    IsActive = t.IsActive
                })
                .ToList();

            // Load existing bank accounts so the operator can edit / remove them.
            model.BankAccounts = customer.BankAccounts
                .OrderBy(b => b.BankName).ThenBy(b => b.AccountNumber)
                .Select(b => new CustomerBankInput
                {
                    Id = b.CustomerBankId,
                    AccountNumber = b.AccountNumber,
                    BankName = b.BankName,
                    BankAddress = b.BankAddress,
                    BankBranch = b.BankBranch,
                    BankSwiftCode = b.BankSwiftCode,
                    IsActive = b.IsActive
                })
                .ToList();

            await PopulateDropdownsAsync(model);
            return View(model);
        }

        // POST: Customer/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, CustomerEditViewModel model)
        {
            if (id != model.Id) return NotFound();

            // Drop empty truck rows BEFORE model validation runs (see Create POST).
            CleanEmptyTruckRows(model);
            CleanEmptyBankRows(model);

            if (model.HasRebate && (!model.RebateAmount.HasValue || model.RebateAmount <= 0))
            {
                ModelState.AddModelError(nameof(model.RebateAmount), "Rebate amount is required when Rebate is enabled.");
            }
            if (model.TransportRequired && (!model.TransportAmount.HasValue || model.TransportAmount <= 0))
            {
                ModelState.AddModelError(nameof(model.TransportAmount), "Transport amount is required when Transport is enabled.");
            }

            if (!string.IsNullOrWhiteSpace(model.Phone) &&
                await _context.Customers.AnyAsync(c => c.Phone == model.Phone && c.Id != model.Id))
            {
                ModelState.AddModelError(nameof(model.Phone), "Another customer with this phone number already exists.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateDropdownsAsync(model);
                return View(model);
            }

            var customer = await _context.Customers
                .Include(c => c.MaterialPrices)
                .Include(c => c.Trucks)
                .Include(c => c.BankAccounts)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (customer == null) return NotFound();

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                customer.Name = model.Name.Trim();
                // Customer Number: keep whatever the form posted back (read-only
                // input echoes the existing value), or generate one if for some
                // reason the customer never had one assigned (legacy data).
                if (string.IsNullOrWhiteSpace(model.RCNumber))
                {
                    customer.RCNumber = string.IsNullOrWhiteSpace(customer.RCNumber)
                        ? await GenerateNextCustomerNumberAsync()
                        : customer.RCNumber;
                }
                else
                {
                    customer.RCNumber = model.RCNumber.Trim();
                }
                customer.Location = model.Location?.Trim() ?? string.Empty;
                customer.LGA = model.LGA?.Trim() ?? string.Empty;
                customer.State = model.State?.Trim() ?? string.Empty;
                customer.MiningLicenseNumber = model.MiningLicenseNumber?.Trim();
                customer.ContactPerson = model.ContactPerson?.Trim();
                customer.Phone = string.IsNullOrWhiteSpace(model.Phone) ? null : model.Phone.Trim();
                customer.Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim();
                customer.TIN = model.TIN?.Trim();
                // BVN field removed from form. Don't overwrite the existing
                // database value with the null that arrives from the unposted
                // input. Leaving the assignment out preserves any previously
                // saved BVN.
                customer.BillingAddress = model.BillingAddress?.Trim();
                customer.CreditLimit = model.CreditLimit;
                customer.Status = string.IsNullOrWhiteSpace(model.Status) ? customer.Status : model.Status;
                customer.CustomerTypeId = model.CustomerTypeId;
                customer.VatTypeId = model.VatTypeId;
                customer.HasRebate = model.HasRebate;
                customer.RebateAmount = model.HasRebate ? model.RebateAmount : null;
                customer.TransportRequired = model.TransportRequired;
                customer.TransportAmount = model.TransportRequired ? model.TransportAmount : null;
                customer.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                // Reconcile per-customer prices. For each submitted row:
                //  - If price/VAT changed from the latest effective, add a new history row.
                //  - If unchanged, do nothing (history stays clean).
                // Rows the operator removed from the form have already been dropped
                // because only posted rows arrive here; we do NOT delete history.
                var submittedPrices = (model.MaterialPrices ?? new())
                    .Where(p => p.MaterialId.HasValue && p.MaterialId > 0 && p.UnitPrice > 0)
                    .ToList();

                foreach (var submitted in submittedPrices)
                {
                    var materialId = submitted.MaterialId!.Value;

                    var latestExisting = customer.MaterialPrices
                        .Where(cmp => cmp.MaterialId == materialId)
                        .OrderByDescending(cmp => cmp.EffectiveFrom)
                        .ThenByDescending(cmp => cmp.Id)
                        .FirstOrDefault();

                    var priceChanged = latestExisting == null
                        || latestExisting.UnitPrice != submitted.UnitPrice
                        || latestExisting.VatRate != submitted.VatRate;

                    if (priceChanged)
                    {
                        await _pricingService.AddOrUpdatePriceAsync(
                            customer.Id,
                            materialId,
                            submitted.UnitPrice,
                            submitted.VatRate,
                            submitted.EffectiveFrom == default ? DateTime.Today : submitted.EffectiveFrom,
                            User.Identity?.Name,
                            submitted.Notes);
                    }
                }
                await _context.SaveChangesAsync();

                // Reconcile per-customer trucks. Submitted rows fully replace
                // the existing list: rows the operator removed in the form are
                // deleted, rows with new values are added or updated. Truck
                // numbers are unique per customer (DB-enforced).
                var submittedTrucks = (model.Trucks ?? new())
                    .Where(t => !string.IsNullOrWhiteSpace(t.CustomerTruckNumber))
                    .GroupBy(t => t.CustomerTruckNumber.Trim(), StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .ToList();

                var submittedIds = submittedTrucks.Where(t => t.Id > 0).Select(t => t.Id).ToHashSet();
                var trucksToRemove = customer.Trucks.Where(t => !submittedIds.Contains(t.CustomerTruckId)).ToList();
                foreach (var truck in trucksToRemove)
                {
                    _context.CustomerTrucks.Remove(truck);
                }

                foreach (var submitted in submittedTrucks)
                {
                    if (submitted.Id > 0)
                    {
                        var existing = customer.Trucks.FirstOrDefault(t => t.CustomerTruckId == submitted.Id);
                        if (existing != null)
                        {
                            existing.CustomerTruckNumber = submitted.CustomerTruckNumber.Trim();
                            existing.IsActive = submitted.IsActive;
                        }
                    }
                    else
                    {
                        _context.CustomerTrucks.Add(new CustomerTruck
                        {
                            CustomerId = customer.Id,
                            CustomerTruckNumber = submitted.CustomerTruckNumber.Trim(),
                            IsActive = submitted.IsActive,
                            CreatedAt = DateTime.Now
                        });
                    }
                }
                await _context.SaveChangesAsync();

                // Reconcile per-customer bank accounts. Same shape as the truck
                // reconciliation above: rows the operator removed in the form
                // are deleted, rows with new values are added or updated. Account
                // numbers are unique per customer (DB-enforced).
                var submittedBanks = (model.BankAccounts ?? new())
                    .Where(b => !string.IsNullOrWhiteSpace(b.AccountNumber) && !string.IsNullOrWhiteSpace(b.BankName))
                    .GroupBy(b => b.AccountNumber.Trim(), StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .ToList();

                var submittedBankIds = submittedBanks.Where(b => b.Id > 0).Select(b => b.Id).ToHashSet();
                var banksToRemove = customer.BankAccounts.Where(b => !submittedBankIds.Contains(b.CustomerBankId)).ToList();
                foreach (var bank in banksToRemove)
                {
                    _context.CustomerBanks.Remove(bank);
                }

                foreach (var submitted in submittedBanks)
                {
                    if (submitted.Id > 0)
                    {
                        var existing = customer.BankAccounts.FirstOrDefault(b => b.CustomerBankId == submitted.Id);
                        if (existing != null)
                        {
                            existing.AccountNumber = submitted.AccountNumber.Trim();
                            existing.BankName = submitted.BankName.Trim();
                            existing.BankAddress = submitted.BankAddress?.Trim();
                            existing.BankBranch = submitted.BankBranch?.Trim();
                            existing.BankSwiftCode = submitted.BankSwiftCode?.Trim();
                            existing.IsActive = submitted.IsActive;
                        }
                    }
                    else
                    {
                        _context.CustomerBanks.Add(new CustomerBank
                        {
                            CustomerId = customer.Id,
                            AccountNumber = submitted.AccountNumber.Trim(),
                            BankName = submitted.BankName.Trim(),
                            BankAddress = submitted.BankAddress?.Trim(),
                            BankBranch = submitted.BankBranch?.Trim(),
                            BankSwiftCode = submitted.BankSwiftCode?.Trim(),
                            IsActive = submitted.IsActive,
                            CreatedAt = DateTime.Now
                        });
                    }
                }
                await _context.SaveChangesAsync();

                await EnsureCustomerLedgerAccountAsync(customer);

                await transaction.CommitAsync();

                TempData["Success"] = $"Customer '{customer.Name}' updated successfully.";
                _logger.LogInformation("Customer {CustomerName} updated by user {UserName}", customer.Name, User.Identity?.Name);

                return RedirectToAction(nameof(Details), new { id = customer.Id });
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync();
                if (!CustomerExists(customer.Id)) return NotFound();
                throw;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error updating customer {CustomerId}", id);
                ModelState.AddModelError(string.Empty, "An error occurred while updating the customer. Please try again.");
                await PopulateDropdownsAsync(model);
                return View(model);
            }
        }

        // GET: Customer/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var customer = await _context.Customers
                .Include(c => c.CustomerType)
                .Include(c => c.VatType)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (customer == null) return NotFound();

            return View(customer);
        }

        // POST: Customer/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var customer = await _context.Customers.FindAsync(id);
                if (customer != null)
                {
                    var hasTransactions = await _context.WeighmentTransactions.AnyAsync(w => w.CustomerId == id);
                    if (hasTransactions)
                    {
                        TempData["Error"] = "Cannot delete customer with existing transactions.";
                        return RedirectToAction(nameof(Index));
                    }

                    _context.Customers.Remove(customer);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = "Customer deleted successfully.";
                    _logger.LogInformation("Customer {CustomerName} deleted by user {UserName}", customer.Name, User.Identity?.Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting customer");
                TempData["Error"] = "An error occurred while deleting the customer.";
            }
            return RedirectToAction(nameof(Index));
        }

        // ----------------------------------------------------------------------
        // AJAX endpoints
        // ----------------------------------------------------------------------

        // Used by Create/Edit to fetch a material's catalog price as a starting point.
        [HttpGet]
        public async Task<JsonResult> GetMaterialCatalog(int materialId)
        {
            var m = await _context.Materials.FirstOrDefaultAsync(x => x.Id == materialId);
            if (m == null) return Json(new { success = false });
            return Json(new { success = true, unitPrice = m.UnitPrice, vatRate = m.VatRate, unit = m.Unit });
        }

        // Used by Weighment/Quotation/Invoice screens to look up the effective
        // price for a (customer, material) pair. Single source of truth.
        [HttpGet]
        public async Task<JsonResult> GetEffectivePrice(int customerId, int materialId)
        {
            var result = await _pricingService.GetPricingAsync(customerId, materialId);
            return Json(new
            {
                success = true,
                unitPrice = result.UnitPrice,
                vatRate = result.VatRate,
                isCustomerSpecific = result.IsCustomerSpecific,
                vatType = result.VatType
            });
        }

        // Credit-limit check used by invoice/weighment flows.
        public async Task<JsonResult> CheckCreditLimit(int customerId, decimal additionalAmount)
        {
            try
            {
                var customer = await _context.Customers.FindAsync(customerId);
                if (customer == null) return Json(new { success = false, message = "Customer not found" });

                var prepaymentBalance = await GetAvailablePrepaymentAsync(customerId);
                var effectiveOutstanding = Math.Max(0, customer.OutstandingBalance - prepaymentBalance);
                var projectedOutstanding = effectiveOutstanding + additionalAmount;
                var exceedsLimit = projectedOutstanding > customer.CreditLimit;

                return Json(new
                {
                    success = true,
                    exceedsLimit,
                    availableCredit = customer.AvailableCredit,
                    currentOutstanding = customer.OutstandingBalance,
                    creditLimit = customer.CreditLimit,
                    prepaymentBalance,
                    effectiveOutstanding,
                    projectedOutstanding
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking credit limit for customer {CustomerId}", customerId);
                return Json(new { success = false, message = "Error checking credit limit" });
            }
        }

        // ----------------------------------------------------------------------
        // Helpers
        // ----------------------------------------------------------------------
        private bool CustomerExists(int id) => _context.Customers.Any(e => e.Id == id);

        /// <summary>
        /// Removes truck rows that have an empty/whitespace truck number from
        /// both the model collection and from ModelState. This lets the operator
        /// add a row in the UI and not fill it in without blocking submit on
        /// the [Required] validator. Truck numbers that ARE filled in still
        /// validate normally.
        /// </summary>
        private void CleanEmptyTruckRows(CustomerCreateViewModel model)
        {
            if (model.Trucks == null || model.Trucks.Count == 0) return;

            for (int i = model.Trucks.Count - 1; i >= 0; i--)
            {
                if (string.IsNullOrWhiteSpace(model.Trucks[i].CustomerTruckNumber))
                {
                    // Drop any ModelState errors registered against this row
                    // (e.g. "Trucks[3].CustomerTruckNumber" required errors).
                    var prefix = $"Trucks[{i}]";
                    var keysToRemove = ModelState.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList();
                    foreach (var key in keysToRemove)
                    {
                        ModelState.Remove(key);
                    }

                    model.Trucks.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Same idea as <see cref="CleanEmptyTruckRows"/>, but for bank-account
        /// rows. A bank row is treated as blank when BOTH AccountNumber and
        /// BankName are missing — that means the operator added a row but
        /// never typed anything. If only one of the two is missing, validation
        /// runs normally and the operator gets a proper error.
        /// </summary>
        private void CleanEmptyBankRows(CustomerCreateViewModel model)
        {
            if (model.BankAccounts == null || model.BankAccounts.Count == 0) return;

            for (int i = model.BankAccounts.Count - 1; i >= 0; i--)
            {
                var row = model.BankAccounts[i];
                if (string.IsNullOrWhiteSpace(row.AccountNumber) && string.IsNullOrWhiteSpace(row.BankName))
                {
                    var prefix = $"BankAccounts[{i}]";
                    var keysToRemove = ModelState.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList();
                    foreach (var key in keysToRemove)
                    {
                        ModelState.Remove(key);
                    }

                    model.BankAccounts.RemoveAt(i);
                }
            }
        }

        private async Task<decimal> GetAvailablePrepaymentAsync(int customerId)
        {
            var prepayments = await _context.CustomerPrepayments
                .Where(p => p.CustomerId == customerId && p.Status == "Active")
                .ToListAsync();
            return prepayments.Sum(p => p.Amount - p.UsedAmount);
        }

        private async Task PopulateDropdownsAsync(CustomerCreateViewModel model)
        {
            model.States = GetNigerianStates();
            model.LGAs = GetNigerianLGAs();
            model.Statuses = GetCustomerStatuses();

            model.CustomerTypes = await _context.CustomerTypes
                .Where(t => t.IsActive)
                .OrderBy(t => t.Id)
                .Select(t => new SelectListItem { Value = t.Id.ToString(), Text = t.Name })
                .ToListAsync();

            model.VatTypes = await _context.VatTypes
                .Where(t => t.IsActive)
                .OrderBy(t => t.Id)
                .Select(t => new SelectListItem { Value = t.Id.ToString(), Text = t.Name })
                .ToListAsync();

            model.Materials = await _context.Materials
                .Where(m => m.Status == "Active")
                .OrderBy(m => m.Name)
                .Select(m => new SelectListItem { Value = m.Id.ToString(), Text = $"{m.Name} ({m.Type})" })
                .ToListAsync();
        }

        private List<SelectListItem> GetNigerianStates()
        {
            return new List<SelectListItem>
            {
                new() { Value = "", Text = "-- Select State --" },
                new() { Value = "Abia", Text = "Abia" },
                new() { Value = "Adamawa", Text = "Adamawa" },
                new() { Value = "Akwa Ibom", Text = "Akwa Ibom" },
                new() { Value = "Anambra", Text = "Anambra" },
                new() { Value = "Bauchi", Text = "Bauchi" },
                new() { Value = "Bayelsa", Text = "Bayelsa" },
                new() { Value = "Benue", Text = "Benue" },
                new() { Value = "Borno", Text = "Borno" },
                new() { Value = "Cross River", Text = "Cross River" },
                new() { Value = "Delta", Text = "Delta" },
                new() { Value = "Ebonyi", Text = "Ebonyi" },
                new() { Value = "Edo", Text = "Edo" },
                new() { Value = "Ekiti", Text = "Ekiti" },
                new() { Value = "Enugu", Text = "Enugu" },
                new() { Value = "FCT", Text = "Federal Capital Territory" },
                new() { Value = "Gombe", Text = "Gombe" },
                new() { Value = "Imo", Text = "Imo" },
                new() { Value = "Jigawa", Text = "Jigawa" },
                new() { Value = "Kaduna", Text = "Kaduna" },
                new() { Value = "Kano", Text = "Kano" },
                new() { Value = "Katsina", Text = "Katsina" },
                new() { Value = "Kebbi", Text = "Kebbi" },
                new() { Value = "Kogi", Text = "Kogi" },
                new() { Value = "Kwara", Text = "Kwara" },
                new() { Value = "Lagos", Text = "Lagos" },
                new() { Value = "Nasarawa", Text = "Nasarawa" },
                new() { Value = "Niger", Text = "Niger" },
                new() { Value = "Ogun", Text = "Ogun" },
                new() { Value = "Ondo", Text = "Ondo" },
                new() { Value = "Osun", Text = "Osun" },
                new() { Value = "Oyo", Text = "Oyo" },
                new() { Value = "Plateau", Text = "Plateau" },
                new() { Value = "Rivers", Text = "Rivers" },
                new() { Value = "Sokoto", Text = "Sokoto" },
                new() { Value = "Taraba", Text = "Taraba" },
                new() { Value = "Yobe", Text = "Yobe" },
                new() { Value = "Zamfara", Text = "Zamfara" }
            };
        }

        private List<SelectListItem> GetNigerianLGAs()
        {
            return new List<SelectListItem>
            {
                new() { Value = "", Text = "-- Select LGA --" },
                new() { Value = "Ikeja", Text = "Ikeja" },
                new() { Value = "Eti-Osa", Text = "Eti-Osa" },
                new() { Value = "Alimosho", Text = "Alimosho" },
                new() { Value = "Kosofe", Text = "Kosofe" },
                new() { Value = "Mushin", Text = "Mushin" },
                new() { Value = "Oshodi-Isolo", Text = "Oshodi-Isolo" },
                new() { Value = "Shomolu", Text = "Shomolu" },
                new() { Value = "Apapa", Text = "Apapa" },
                new() { Value = "Lagos Island", Text = "Lagos Island" },
                new() { Value = "Lagos Mainland", Text = "Lagos Mainland" }
            };
        }

        private List<SelectListItem> GetCustomerStatuses()
        {
            return new List<SelectListItem>
            {
                new() { Value = "", Text = "-- Select Status --" },
                new() { Value = "Active", Text = "Active" },
                new() { Value = "Inactive", Text = "Inactive" },
                new() { Value = "Blacklisted", Text = "Blacklisted" }
            };
        }

        private string GenerateCustomerAccountCode(int customerId) => $"1101-{customerId:D6}";

        /// <summary>
        /// Returns the next sequential customer number in the format CUST-NNNNNN.
        /// Strategy: find the highest existing value matching the CUST-* pattern,
        /// parse the numeric tail, increment, zero-pad to 6 digits. Records that
        /// already carry a non-CUST identifier (legacy RC numbers like RC123456)
        /// are intentionally ignored — they keep their existing value but do
        /// not contribute to the running counter. If two near-simultaneous
        /// creates would collide, a unique-index conflict at the DB layer would
        /// surface as a save error and the operator can retry; this method does
        /// NOT add an index because there isn't one today and one accidental
        /// collision per millennium isn't worth a schema change.
        /// </summary>
        private async Task<string> GenerateNextCustomerNumberAsync()
        {
            const string prefix = "CUST-";
            const int padWidth = 6;

            var existing = await _context.Customers
                .Where(c => c.RCNumber != null && c.RCNumber.StartsWith(prefix))
                .Select(c => c.RCNumber!)
                .ToListAsync();

            int max = 0;
            foreach (var code in existing)
            {
                if (code.Length <= prefix.Length) continue;
                var tail = code.Substring(prefix.Length);
                if (int.TryParse(tail, out var n) && n > max) max = n;
            }

            return $"{prefix}{(max + 1).ToString(new string('0', padWidth))}";
        }

        private async Task EnsureCustomerLedgerAccountAsync(Customer customer)
        {
            try
            {
                var accountCode = GenerateCustomerAccountCode(customer.Id);

                var existing = await _context.ChartOfAccounts.FirstOrDefaultAsync(a => a.AccountCode == accountCode);
                if (existing != null)
                {
                    var desiredName = $"Accounts Receivable - {customer.Name}";
                    var desiredActive = customer.Status == "Active";
                    if (!string.Equals(existing.AccountName, desiredName, StringComparison.Ordinal) ||
                        existing.IsActive != desiredActive ||
                        existing.CurrentBalance != customer.OutstandingBalance)
                    {
                        existing.AccountName = desiredName;
                        existing.IsActive = desiredActive;
                        existing.CurrentBalance = customer.OutstandingBalance;
                        await _context.SaveChangesAsync();
                    }
                    return;
                }

                var account = new ChartOfAccounts
                {
                    AccountCode = accountCode,
                    AccountName = $"Accounts Receivable - {customer.Name}",
                    AccountType = "Asset",
                    SubType = "Current",
                    OpeningBalance = 0m,
                    CurrentBalance = customer.OutstandingBalance,
                    IsActive = customer.Status == "Active",
                    CreatedAt = DateTime.Now
                };

                _context.ChartOfAccounts.Add(account);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Created ledger account {AccountCode} for customer {CustomerId}", accountCode, customer.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating or syncing ledger account for customer {CustomerId}", customer.Id);
            }
        }
    }
}
