using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuarryManagementSystem.Data;
using QuarryManagementSystem.Models.Domain;

namespace QuarryManagementSystem.Controllers
{
    [Authorize(Roles = "Admin,Manager,Accountant")]
    public class ChartOfAccountsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ChartOfAccountsController> _logger;

        public ChartOfAccountsController(ApplicationDbContext context, ILogger<ChartOfAccountsController> logger)
        {
            _context = context;
            _logger = logger;
        }

                // GET: ChartOfAccounts
                public async Task<IActionResult> Index()
                {
                    // Ensure CurrentBalance reflects all journal entries before displaying
                    await RecalculateAllAccountBalancesAsync();
        
                    var accounts = await _context.ChartOfAccounts
                        .OrderBy(a => a.AccountCode)
                        .ToListAsync();
        
                    return View(accounts);
                }
        
                /// <summary>
                /// Recalculates CurrentBalance for all accounts from their journal entry lines
                /// plus OpeningBalance. This keeps the Chart of Accounts in sync even if
                /// some operations forgot to update balances explicitly.
                /// </summary>
                private async Task RecalculateAllAccountBalancesAsync()
                {
                    // Get all accounts so helper methods like IsAssetAccount/IsExpenseAccount can be used
                    var accounts = await _context.ChartOfAccounts.ToListAsync();

                    // Aggregate debits and credits per account from all journal lines
                    var movements = await _context.JournalEntryLines
                        .GroupBy(l => l.AccountId)
                        .Select(g => new
                        {
                            AccountId = g.Key,
                            Debit = g.Sum(l => l.DebitAmount),
                            Credit = g.Sum(l => l.CreditAmount)
                        })
                        .ToListAsync();

                    var movementLookup = movements.ToDictionary(x => x.AccountId, x => x);

                    // Pre-calculate customer prepayment wallet balances from CustomerPrepayments
                    // so that per-customer prepayment accounts (2103-000007 etc.) show the correct
                    // amount even if something went wrong with journal postings.
                    var customerPrepaymentBalances = await _context.CustomerPrepayments
                        .Where(p => p.Status == "Active")
                        .GroupBy(p => p.CustomerId)
                        .Select(g => new
                        {
                            CustomerId = g.Key,
                            Balance = g.Sum(p => p.Amount - p.UsedAmount)
                        })
                        .ToDictionaryAsync(x => x.CustomerId, x => x.Balance);

                    foreach (var account in accounts)
                    {
                        // Special handling for customer-specific prepayment accounts:
                        // AccountCode pattern "2103-000007" => use wallet balance, not journal lines.
                        if (account.AccountCode != null && account.AccountCode.StartsWith("2103-"))
                        {
                            var suffix = account.AccountCode.Substring("2103-".Length);
                            if (int.TryParse(suffix, out var customerId) &&
                                customerPrepaymentBalances.TryGetValue(customerId, out var walletBalance))
                            {
                                account.CurrentBalance = walletBalance;
                                continue;
                            }
                        }

                        movementLookup.TryGetValue(account.Id, out var totals);

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

                    await _context.SaveChangesAsync();
                }

        // GET: ChartOfAccounts/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var account = await _context.ChartOfAccounts
                .FirstOrDefaultAsync(m => m.Id == id);

            if (account == null)
            {
                return NotFound();
            }

            return View(account);
        }
    }
}