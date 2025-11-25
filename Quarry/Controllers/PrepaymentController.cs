using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuarryManagementSystem.Data;
using QuarryManagementSystem.Models.Domain;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace QuarryManagementSystem.Controllers
{
    [Authorize(Roles = "Admin,Manager,Accountant")]
    public class PrepaymentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<PrepaymentController> _logger;

        public PrepaymentController(ApplicationDbContext context, ILogger<PrepaymentController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: Prepayment
        public async Task<IActionResult> Index(int? customerId, DateTime? fromDate, DateTime? toDate)
        {
            var query = _context.CustomerPrepayments
                .Include(p => p.Customer)
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

            ViewBag.Customers = await _context.Customers
                .OrderBy(c => c.Name)
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.Name
                })
                .ToListAsync();

            return View(prepayments);
        }

        // GET: Prepayment/Create
        public async Task<IActionResult> Create()
        {
            await PopulateCustomersAsync();

            var model = new CustomerPrepayment
            {
                PrepaymentDate = DateTime.Now
            };

            return View(model);
        }

        // POST: Prepayment/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CustomerPrepayment model)
        {
            try
            {
                // PrepaymentNumber is system-generated, not entered by user,
                // so remove it from model validation before checking ModelState.
                ModelState.Remove(nameof(CustomerPrepayment.PrepaymentNumber));

                if (!ModelState.IsValid)
                {
                    await PopulateCustomersAsync();
                    return View(model);
                }

                if (model.CustomerId <= 0)
                {
                    ModelState.AddModelError("CustomerId", "Please select a customer.");
                    await PopulateCustomersAsync();
                    return View(model);
                }

                if (model.Amount <= 0)
                {
                    ModelState.AddModelError("Amount", "Prepayment amount must be greater than zero.");
                    await PopulateCustomersAsync();
                    return View(model);
                }

                model.PrepaymentNumber = await GeneratePrepaymentNumberAsync();
                model.Status = "Active";
                model.CreatedAt = DateTime.Now;
                model.CreatedBy = User.Identity?.Name;
                model.UsedAmount = 0;

                _context.CustomerPrepayments.Add(model);
                await _context.SaveChangesAsync();

                await CreatePrepaymentJournalEntryAsync(model);

                TempData["Success"] = $"Prepayment {model.PrepaymentNumber} created successfully.";
                _logger.LogInformation("Prepayment {PrepaymentNumber} created for customer {CustomerId} by {UserName}",
                    model.PrepaymentNumber, model.CustomerId, User.Identity?.Name);

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating prepayment");
                ModelState.AddModelError(string.Empty, "An error occurred while creating the prepayment. Please try again.");
            }

            await PopulateCustomersAsync();
            return View(model);
        }

        private async Task PopulateCustomersAsync()
        {
            ViewBag.Customers = await _context.Customers
                .OrderBy(c => c.Name)
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.Name
                })
                .ToListAsync();
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
                var lastNumberStr = last.PrepaymentNumber.Substring(prefix.Length);
                if (int.TryParse(lastNumberStr, out var lastNumber))
                {
                    nextNumber = lastNumber + 1;
                }
            }

            return $"{prefix}{nextNumber:D4}";
        }

        private async Task CreatePrepaymentJournalEntryAsync(CustomerPrepayment prepayment)
        {
            try
            {
                var entryNumber = JournalEntry.GenerateEntryNumber("ADV");

                // Resolve account ids once so we can also recalculate their balances later
                var cashAccountId = await GetCashAccountId();
                var customerPrepaymentAccountId = await GetCustomerPrepaymentAccountId();

                var journalEntry = new JournalEntry
                {
                    EntryNumber = entryNumber,
                    EntryDate = prepayment.PrepaymentDate,
                    Reference = $"Prepayment {prepayment.PrepaymentNumber}",
                    Description = $"Customer prepayment of {prepayment.Amount:C} for customer {prepayment.CustomerId}",
                    PostedBy = User.Identity?.Name,
                    IsAutoGenerated = true,
                    CreatedAt = DateTime.Now
                };

                // Debit Cash/Bank
                journalEntry.JournalEntryLines.Add(new JournalEntryLine
                {
                    AccountId = cashAccountId,
                    DebitAmount = prepayment.Amount,
                    CreditAmount = 0,
                    LineDescription = $"Prepayment received - {prepayment.PrepaymentNumber}"
                });

                // Credit Customer Prepayments (liability)
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

                // After the journal entry is saved, recalculate balances for the affected accounts
                await RecalculateAccountBalanceAsync(cashAccountId);
                await RecalculateAccountBalanceAsync(customerPrepaymentAccountId);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating journal entry for prepayment");
            }
        }

        /// <summary>
        /// Recalculates a single account's CurrentBalance from all its journal entry lines
        /// plus its OpeningBalance. This guarantees the Chart of Accounts ledger is accurate.
        /// </summary>
        private async Task RecalculateAccountBalanceAsync(int accountId)
        {
            var account = await _context.ChartOfAccounts
                .FirstOrDefaultAsync(a => a.Id == accountId);

            if (account == null)
                return;

            var totals = await _context.JournalEntryLines
                .Where(l => l.AccountId == accountId)
                .GroupBy(l => l.AccountId)
                .Select(g => new
                {
                    Debit = g.Sum(l => l.DebitAmount),
                    Credit = g.Sum(l => l.CreditAmount)
                })
                .FirstOrDefaultAsync();

            decimal totalDebit = totals?.Debit ?? 0;
            decimal totalCredit = totals?.Credit ?? 0;

            decimal netMovement;

            if (account.IsAssetAccount() || account.IsExpenseAccount())
            {
                // Assets & Expenses: debit increases, credit decreases
                netMovement = totalDebit - totalCredit;
            }
            else
            {
                // Liabilities, Equity, Revenue: credit increases, debit decreases
                netMovement = totalCredit - totalDebit;
            }

            account.CurrentBalance = account.OpeningBalance + netMovement;
        }

        private async Task<int> GetCashAccountId()
        {
            var cashAccount = await _context.ChartOfAccounts
                .FirstOrDefaultAsync(ca => ca.AccountCode == "1001");
            return cashAccount?.Id ?? 1;
        }

        private async Task<int> GetCustomerPrepaymentAccountId()
        {
            var prepayAccount = await _context.ChartOfAccounts
                .FirstOrDefaultAsync(ca => ca.AccountCode == "2103");
            return prepayAccount?.Id ?? 5;
        }
    }
}