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
    [Authorize(Roles = "Admin,Manager,Accountant")]
    public class CustomerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CustomerController> _logger;

        public CustomerController(ApplicationDbContext context, ILogger<CustomerController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: Customer
        public async Task<IActionResult> Index(string searchTerm, string state, string status, int page = 1)
        {
            try
            {
                int pageSize = 20;
                var query = _context.Customers.AsQueryable();

                // Apply filters
                if (!string.IsNullOrEmpty(searchTerm))
                {
                    query = query.Where(c => 
                        c.Name.Contains(searchTerm) || 
                        c.ContactPerson.Contains(searchTerm) ||
                        c.Phone.Contains(searchTerm) ||
                        c.Email.Contains(searchTerm));
                }

                if (!string.IsNullOrEmpty(state))
                {
                    query = query.Where(c => c.State == state);
                }

                if (!string.IsNullOrEmpty(status))
                {
                    query = query.Where(c => c.Status == status);
                }

                // Get total count for pagination
                var totalCount = await query.CountAsync();
                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

                // Apply pagination
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
            if (id == null)
            {
                return NotFound();
            }

            var customer = await _context.Customers
                .Include(c => c.WeighmentTransactions)
                .ThenInclude(w => w.Material)
                .Include(c => c.Invoices)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (customer == null)
            {
                return NotFound();
            }

            return View(customer);
        }

        // GET: Customer/Create
        public IActionResult Create()
        {
            ViewBag.States = GetNigerianStates();
            ViewBag.LGAs = GetNigerianLGAs();
            ViewBag.Statuses = GetCustomerStatuses();
            return View();
        }

        // POST: Customer/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Customer customer)
        {
            try
            {
                // Check if phone number already exists
                if (await _context.Customers.AnyAsync(c => c.Phone == customer.Phone))
                {
                    ModelState.AddModelError("Phone", "A customer with this phone number already exists.");
                }

                if (ModelState.IsValid)
                {
                    customer.CreatedAt = DateTime.Now;
                    customer.OutstandingBalance = 0;
                    customer.Status = "Active";

                    _context.Add(customer);
                    await _context.SaveChangesAsync();

                    // Create a dedicated ledger account for this customer in Chart of Accounts
                    await EnsureCustomerLedgerAccountAsync(customer);

                    TempData["Success"] = "Customer created successfully.";
                    _logger.LogInformation("Customer {CustomerName} created by user {UserName}",
                        customer.Name, User.Identity?.Name);
                    
                    return RedirectToAction(nameof(Index));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating customer");
                ModelState.AddModelError("", "An error occurred while creating the customer. Please try again.");
            }

            ViewBag.States = GetNigerianStates();
            ViewBag.LGAs = GetNigerianLGAs();
            ViewBag.Statuses = GetCustomerStatuses();
            return View(customer);
        }

        // GET: Customer/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var customer = await _context.Customers.FindAsync(id);
            if (customer == null)
            {
                return NotFound();
            }

            ViewBag.States = GetNigerianStates();
            ViewBag.LGAs = GetNigerianLGAs();
            ViewBag.Statuses = GetCustomerStatuses();
            return View(customer);
        }

        // POST: Customer/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Customer customer)
        {
            if (id != customer.Id)
            {
                return NotFound();
            }

            try
            {
                // Check if phone number already exists for another customer
                if (await _context.Customers.AnyAsync(c => c.Phone == customer.Phone && c.Id != customer.Id))
                {
                    ModelState.AddModelError("Phone", "Another customer with this phone number already exists.");
                }

                if (ModelState.IsValid)
                {
                    try
                    {
                        customer.UpdatedAt = DateTime.Now;
                        _context.Update(customer);
                        await _context.SaveChangesAsync();

                        TempData["Success"] = "Customer updated successfully.";
                        _logger.LogInformation("Customer {CustomerName} updated by user {UserName}", 
                            customer.Name, User.Identity?.Name);
                    }
                    catch (DbUpdateConcurrencyException)
                    {
                        if (!CustomerExists(customer.Id))
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating customer");
                ModelState.AddModelError("", "An error occurred while updating the customer. Please try again.");
            }

            ViewBag.States = GetNigerianStates();
            ViewBag.LGAs = GetNigerianLGAs();
            ViewBag.Statuses = GetCustomerStatuses();
            return View(customer);
        }

        // GET: Customer/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var customer = await _context.Customers
                .FirstOrDefaultAsync(m => m.Id == id);
            if (customer == null)
            {
                return NotFound();
            }

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
                    // Check if customer has any transactions
                    var hasTransactions = await _context.WeighmentTransactions
                        .AnyAsync(w => w.CustomerId == id);
                    
                    if (hasTransactions)
                    {
                        TempData["Error"] = "Cannot delete customer with existing transactions.";
                        return RedirectToAction(nameof(Index));
                    }

                    _context.Customers.Remove(customer);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = "Customer deleted successfully.";
                    _logger.LogInformation("Customer {CustomerName} deleted by user {UserName}", 
                        customer.Name, User.Identity?.Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting customer");
                TempData["Error"] = "An error occurred while deleting the customer.";
            }
            return RedirectToAction(nameof(Index));
        }

        // GET: Customer/CheckCreditLimit/5
        public async Task<JsonResult> CheckCreditLimit(int customerId, decimal additionalAmount)
        {
            try
            {
                var customer = await _context.Customers.FindAsync(customerId);
                if (customer == null)
                {
                    return Json(new { success = false, message = "Customer not found" });
                }

                // Include prepayment wallet when evaluating credit limit
                var prepaymentBalance = await GetAvailablePrepaymentAsync(customerId);

                var effectiveOutstanding = customer.OutstandingBalance - prepaymentBalance;
                if (effectiveOutstanding < 0)
                {
                    effectiveOutstanding = 0;
                }

                var projectedOutstanding = effectiveOutstanding + additionalAmount;
                var exceedsLimit = projectedOutstanding > customer.CreditLimit;
                var availableCredit = customer.AvailableCredit;

                return Json(new
                {
                    success = true,
                    exceedsLimit,
                    availableCredit,
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
 
        // Helper methods
        private bool CustomerExists(int id)
        {
            return _context.Customers.Any(e => e.Id == id);
        }

        private async Task<decimal> GetAvailablePrepaymentAsync(int customerId)
        {
            var prepayments = await _context.CustomerPrepayments
                .Where(p => p.CustomerId == customerId && p.Status == "Active")
                .ToListAsync();

            return prepayments.Sum(p => p.Amount - p.UsedAmount);
        }

        private List<SelectListItem> GetNigerianStates()
        {
            return new List<SelectListItem>
            {
                new SelectListItem { Value = "", Text = "-- Select State --" },
                new SelectListItem { Value = "Abia", Text = "Abia" },
                new SelectListItem { Value = "Adamawa", Text = "Adamawa" },
                new SelectListItem { Value = "Akwa Ibom", Text = "Akwa Ibom" },
                new SelectListItem { Value = "Anambra", Text = "Anambra" },
                new SelectListItem { Value = "Bauchi", Text = "Bauchi" },
                new SelectListItem { Value = "Bayelsa", Text = "Bayelsa" },
                new SelectListItem { Value = "Benue", Text = "Benue" },
                new SelectListItem { Value = "Borno", Text = "Borno" },
                new SelectListItem { Value = "Cross River", Text = "Cross River" },
                new SelectListItem { Value = "Delta", Text = "Delta" },
                new SelectListItem { Value = "Ebonyi", Text = "Ebonyi" },
                new SelectListItem { Value = "Edo", Text = "Edo" },
                new SelectListItem { Value = "Ekiti", Text = "Ekiti" },
                new SelectListItem { Value = "Enugu", Text = "Enugu" },
                new SelectListItem { Value = "FCT", Text = "Federal Capital Territory" },
                new SelectListItem { Value = "Gombe", Text = "Gombe" },
                new SelectListItem { Value = "Imo", Text = "Imo" },
                new SelectListItem { Value = "Jigawa", Text = "Jigawa" },
                new SelectListItem { Value = "Kaduna", Text = "Kaduna" },
                new SelectListItem { Value = "Kano", Text = "Kano" },
                new SelectListItem { Value = "Katsina", Text = "Katsina" },
                new SelectListItem { Value = "Kebbi", Text = "Kebbi" },
                new SelectListItem { Value = "Kogi", Text = "Kogi" },
                new SelectListItem { Value = "Kwara", Text = "Kwara" },
                new SelectListItem { Value = "Lagos", Text = "Lagos" },
                new SelectListItem { Value = "Nasarawa", Text = "Nasarawa" },
                new SelectListItem { Value = "Niger", Text = "Niger" },
                new SelectListItem { Value = "Ogun", Text = "Ogun" },
                new SelectListItem { Value = "Ondo", Text = "Ondo" },
                new SelectListItem { Value = "Osun", Text = "Osun" },
                new SelectListItem { Value = "Oyo", Text = "Oyo" },
                new SelectListItem { Value = "Plateau", Text = "Plateau" },
                new SelectListItem { Value = "Rivers", Text = "Rivers" },
                new SelectListItem { Value = "Sokoto", Text = "Sokoto" },
                new SelectListItem { Value = "Taraba", Text = "Taraba" },
                new SelectListItem { Value = "Yobe", Text = "Yobe" },
                new SelectListItem { Value = "Zamfara", Text = "Zamfara" }
            };
        }

        private List<SelectListItem> GetNigerianLGAs()
        {
            return new List<SelectListItem>
            {
                new SelectListItem { Value = "", Text = "-- Select LGA --" },
                // Add major LGAs for each state
                new SelectListItem { Value = "Ikeja", Text = "Ikeja" },
                new SelectListItem { Value = "Eti-Osa", Text = "Eti-Osa" },
                new SelectListItem { Value = "Alimosho", Text = "Alimosho" },
                new SelectListItem { Value = "Kosofe", Text = "Kosofe" },
                new SelectListItem { Value = "Mushin", Text = "Mushin" },
                new SelectListItem { Value = "Oshodi-Isolo", Text = "Oshodi-Isolo" },
                new SelectListItem { Value = "Shomolu", Text = "Shomolu" },
                new SelectListItem { Value = "Apapa", Text = "Apapa" },
                new SelectListItem { Value = "Lagos Island", Text = "Lagos Island" },
                new SelectListItem { Value = "Lagos Mainland", Text = "Lagos Mainland" }
                // Add more LGAs as needed
            };
        }

        private List<SelectListItem> GetCustomerStatuses()
        {
            return new List<SelectListItem>
            {
                new SelectListItem { Value = "", Text = "-- Select Status --" },
                new SelectListItem { Value = "Active", Text = "Active" },
                new SelectListItem { Value = "Inactive", Text = "Inactive" },
                new SelectListItem { Value = "Blacklisted", Text = "Blacklisted" }
            };
        }

        // Ledger helpers: create/sync a Chart of Accounts entry for each customer
        private string GenerateCustomerAccountCode(int customerId)
        {
            // Accounts Receivable base account is 1101; append zero-padded customer id
            return $"1101-{customerId:D6}";
        }

        private async Task EnsureCustomerLedgerAccountAsync(Customer customer)
        {
            try
            {
                var accountCode = GenerateCustomerAccountCode(customer.Id);

                var existing = await _context.ChartOfAccounts
                    .FirstOrDefaultAsync(a => a.AccountCode == accountCode);

                if (existing != null)
                {
                    // Keep in sync with latest customer info
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
                // Do not block customer creation if ledger fails; can be addressed later by admins
            }
        }
    }
}