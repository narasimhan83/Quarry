using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuarryManagementSystem.Data;
using QuarryManagementSystem.Models;
using QuarryManagementSystem.Models.Domain;
using QuarryManagementSystem.ViewModels;

namespace QuarryManagementSystem.Controllers
{
    [Authorize(Roles = "Admin,Manager,Accountant")]
    public class OpeningBalanceController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<OpeningBalanceController> _logger;
        private readonly UserManager<ApplicationUser> _userManager;

        // Fallback account code used to balance the opening balance journal entry
        // when a user's opening balances don't net to zero on their own.
        private const string OpeningBalanceEquityCode = "3102";

        public OpeningBalanceController(
            ApplicationDbContext context,
            ILogger<OpeningBalanceController> logger,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _logger = logger;
            _userManager = userManager;
        }

        // GET: OpeningBalance
        // Maintain opening balances per account for a selected fiscal year
        public async Task<IActionResult> Index(int? fiscalYearId)
        {
            var fiscalYears = await _context.FiscalYears
                .OrderBy(fy => fy.StartDate)
                .ToListAsync();

            if (!fiscalYears.Any())
            {
                TempData["Error"] = "No fiscal years defined. Please create a fiscal year first.";
                return RedirectToAction("Index", "FiscalYear");
            }

            var selectedFiscalYear = fiscalYearId.HasValue
                ? fiscalYears.FirstOrDefault(fy => fy.Id == fiscalYearId.Value)
                : fiscalYears.FirstOrDefault(fy => fy.IsCurrent) ?? fiscalYears.First();

            if (selectedFiscalYear == null)
            {
                selectedFiscalYear = fiscalYears.First();
            }

            var viewModel = new OpeningBalanceFiscalYearViewModel
            {
                FiscalYearId = selectedFiscalYear.Id,
                FiscalYearName = $"{selectedFiscalYear.YearCode} ({selectedFiscalYear.StartDate:dd/MM/yyyy} - {selectedFiscalYear.EndDate:dd/MM/yyyy})",
                FiscalYears = fiscalYears.Select(fy => new SelectListItem
                {
                    Value = fy.Id.ToString(),
                    Text = fy.YearCode,
                    Selected = fy.Id == selectedFiscalYear.Id
                }).ToList()
            };

            var accounts = await _context.ChartOfAccounts
                .OrderBy(a => a.AccountCode)
                .ToListAsync();

            var balances = await _context.AccountFiscalYearBalances
                .Where(b => b.FiscalYearId == selectedFiscalYear.Id)
                .ToListAsync();

            var balanceLookup = balances.ToDictionary(b => b.AccountId, b => b);

            foreach (var account in accounts)
            {
                balanceLookup.TryGetValue(account.Id, out var balance);

                // Prefer the per-fiscal-year balance if one exists; otherwise fall back to
                // the legacy global OpeningBalance on ChartOfAccounts.
                var opening = balance?.OpeningBalance ?? account.OpeningBalance;

                viewModel.Accounts.Add(new OpeningBalanceAccountRow
                {
                    AccountId = account.Id,
                    AccountCode = account.AccountCode,
                    AccountName = account.AccountName,
                    AccountType = account.AccountType,
                    SubType = account.SubType,
                    OpeningBalance = opening
                });
            }

            return View(viewModel);
        }

        // POST: OpeningBalance/Save
        //
        // Proper double-entry accounting implementation:
        //   1. Upsert per-fiscal-year balances into AccountFiscalYearBalances (audit/history).
        //   2. Remove any prior opening-balance journal entry for this fiscal year.
        //   3. Post a single balanced journal entry on the fiscal year start date with
        //      lines for every non-zero opening balance. Asset/Expense accounts are
        //      debited, Liability/Equity/Revenue accounts are credited.
        //   4. If the entered opening balances do not net to zero, the difference is
        //      posted to the "Opening Balance Equity" (3102) account so the entry
        //      remains balanced (debits == credits).
        //   5. Clear the legacy ChartOfAccounts.OpeningBalance column so the
        //      Current Balance recomputation doesn't double-count.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(OpeningBalanceFiscalYearViewModel model)
        {
            _logger.LogInformation("OpeningBalance.Save called for FiscalYearId {FiscalYearId} with {AccountCount} rows.",
                model.FiscalYearId,
                model.Accounts?.Count ?? 0);

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("OpeningBalance.Save ModelState invalid. Errors: {Errors}",
                    string.Join(" | ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));

                await PopulateFiscalYearDropdownAsync(model);
                return View("Index", model);
            }

            var fiscalYear = await _context.FiscalYears.FindAsync(model.FiscalYearId);
            if (fiscalYear == null)
            {
                _logger.LogWarning("OpeningBalance.Save: FiscalYear {FiscalYearId} not found.", model.FiscalYearId);

                ModelState.AddModelError(string.Empty, "Selected fiscal year does not exist.");
                await PopulateFiscalYearDropdownAsync(model);
                return View("Index", model);
            }

            if (fiscalYear.IsClosed)
            {
                _logger.LogWarning("OpeningBalance.Save: FiscalYear {FiscalYearId} is closed and cannot be modified.", model.FiscalYearId);

                ModelState.AddModelError(string.Empty, "This fiscal year is closed and cannot be modified.");
                await PopulateFiscalYearDropdownAsync(model);
                return View("Index", model);
            }

            // Wrap everything in a transaction so partial failures don't leave the
            // accounts in an inconsistent state.
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // --- Step 1: upsert per-fiscal-year balances -----------------------
                var accountIds = (model.Accounts ?? new List<OpeningBalanceAccountRow>())
                    .Select(a => a.AccountId)
                    .ToList();

                var existingBalances = await _context.AccountFiscalYearBalances
                    .Where(b => b.FiscalYearId == model.FiscalYearId && accountIds.Contains(b.AccountId))
                    .ToListAsync();

                var existingLookup = existingBalances.ToDictionary(b => b.AccountId, b => b);

                int updated = 0;
                int inserted = 0;

                foreach (var row in model.Accounts!)
                {
                    if (existingLookup.TryGetValue(row.AccountId, out var balance))
                    {
                        balance.OpeningBalance = row.OpeningBalance;
                        updated++;
                    }
                    else
                    {
                        _context.AccountFiscalYearBalances.Add(new AccountFiscalYearBalance
                        {
                            AccountId = row.AccountId,
                            FiscalYearId = model.FiscalYearId,
                            OpeningBalance = row.OpeningBalance
                        });
                        inserted++;
                    }
                }

                await _context.SaveChangesAsync();

                // --- Step 2: remove any prior opening-balance journal entry for this FY
                var obEntryPrefix = $"OB/{fiscalYear.YearCode}/";

                var priorEntries = await _context.JournalEntries
                    .Include(je => je.JournalEntryLines)
                    .Where(je => je.EntryNumber.StartsWith(obEntryPrefix))
                    .ToListAsync();

                if (priorEntries.Any())
                {
                    foreach (var prior in priorEntries)
                    {
                        _context.JournalEntryLines.RemoveRange(prior.JournalEntryLines);
                    }
                    _context.JournalEntries.RemoveRange(priorEntries);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("OpeningBalance.Save: removed {Count} prior opening-balance journal entries for FY {YearCode}.",
                        priorEntries.Count, fiscalYear.YearCode);
                }

                // --- Step 3: post a new balanced journal entry -----------------------
                var accounts = await _context.ChartOfAccounts
                    .Where(a => accountIds.Contains(a.Id))
                    .ToListAsync();
                var accountLookup = accounts.ToDictionary(a => a.Id, a => a);

                // Ensure the balancing equity account exists
                var obEquityAccount = await _context.ChartOfAccounts
                    .FirstOrDefaultAsync(a => a.AccountCode == OpeningBalanceEquityCode);

                if (obEquityAccount == null)
                {
                    obEquityAccount = new ChartOfAccounts
                    {
                        AccountCode = OpeningBalanceEquityCode,
                        AccountName = "Opening Balance Equity",
                        AccountType = "Equity",
                        SubType = "Capital",
                        OpeningBalance = 0m,
                        CurrentBalance = 0m,
                        IsActive = true,
                        CreatedAt = DateTime.Now
                    };
                    _context.ChartOfAccounts.Add(obEquityAccount);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("OpeningBalance.Save: created missing Opening Balance Equity account (3102).");
                }

                var journalLines = new List<JournalEntryLine>();
                decimal totalDebit = 0m;
                decimal totalCredit = 0m;

                foreach (var row in model.Accounts)
                {
                    if (row.OpeningBalance == 0m) continue;
                    if (!accountLookup.TryGetValue(row.AccountId, out var acct)) continue;

                    // Skip the balancing equity account itself here; it gets a balancing
                    // line added at the end based on the difference.
                    if (acct.AccountCode == OpeningBalanceEquityCode) continue;

                    var isNaturalDebit = acct.IsAssetAccount() || acct.IsExpenseAccount();

                    var line = new JournalEntryLine
                    {
                        AccountId = row.AccountId,
                        LineDescription = $"Opening balance for {acct.AccountCode} {acct.AccountName} (FY {fiscalYear.YearCode})"
                    };

                    if (row.OpeningBalance > 0)
                    {
                        if (isNaturalDebit)
                        {
                            line.DebitAmount = row.OpeningBalance;
                            totalDebit += row.OpeningBalance;
                        }
                        else
                        {
                            line.CreditAmount = row.OpeningBalance;
                            totalCredit += row.OpeningBalance;
                        }
                    }
                    else
                    {
                        // Negative opening balance flips the side (rare but valid,
                        // e.g. a contra asset or an overdrawn bank account).
                        var absValue = Math.Abs(row.OpeningBalance);
                        if (isNaturalDebit)
                        {
                            line.CreditAmount = absValue;
                            totalCredit += absValue;
                        }
                        else
                        {
                            line.DebitAmount = absValue;
                            totalDebit += absValue;
                        }
                    }

                    journalLines.Add(line);
                }

                // Only post a journal entry if there is something to post
                string? postedEntryNumber = null;
                decimal equityBalancingAmount = 0m;

                if (journalLines.Any())
                {
                    // Balance the entry with a line against Opening Balance Equity
                    var difference = totalDebit - totalCredit;
                    if (difference != 0m)
                    {
                        var balancingLine = new JournalEntryLine
                        {
                            AccountId = obEquityAccount.Id,
                            LineDescription = $"Balancing entry to Opening Balance Equity (FY {fiscalYear.YearCode})"
                        };

                        if (difference > 0)
                        {
                            // More debits than credits → credit OB Equity
                            balancingLine.CreditAmount = difference;
                            totalCredit += difference;
                        }
                        else
                        {
                            // More credits than debits → debit OB Equity
                            balancingLine.DebitAmount = -difference;
                            totalDebit += -difference;
                        }

                        equityBalancingAmount = Math.Abs(difference);
                        journalLines.Add(balancingLine);
                    }

                    postedEntryNumber = $"{obEntryPrefix}{DateTime.Now:HHmmss}";

                    // Resolve the current user's Id (AspNetUsers.Id GUID) for the
                    // FK_JournalEntries_AspNetUsers_PostedBy foreign key. Falls back
                    // to null if no logged-in user is resolvable (PostedBy is nullable).
                    var postedByUserId = _userManager.GetUserId(User);

                    var journalEntry = new JournalEntry
                    {
                        EntryNumber = postedEntryNumber,
                        EntryDate = fiscalYear.StartDate,
                        Reference = $"Opening Balance - FY {fiscalYear.YearCode}",
                        Description = $"Opening balances for fiscal year {fiscalYear.YearCode} " +
                                      $"({fiscalYear.StartDate:dd/MM/yyyy} - {fiscalYear.EndDate:dd/MM/yyyy}). " +
                                      $"Auto-generated from the Opening Balances screen.",
                        TotalDebit = totalDebit,
                        TotalCredit = totalCredit,
                        PostedBy = postedByUserId,
                        IsAutoGenerated = true,
                        CreatedAt = DateTime.Now,
                        JournalEntryLines = journalLines
                    };

                    _context.JournalEntries.Add(journalEntry);
                    await _context.SaveChangesAsync();
                }

                // --- Step 4: clear the legacy global OpeningBalance column so the
                // Chart of Accounts Current Balance calculation doesn't double-count.
                // The journal entry IS the opening balance now.
                foreach (var acct in accounts)
                {
                    if (acct.OpeningBalance != 0m)
                    {
                        acct.OpeningBalance = 0m;
                    }
                }
                // Include the OB Equity account in the clearing pass too
                if (obEquityAccount.OpeningBalance != 0m)
                {
                    obEquityAccount.OpeningBalance = 0m;
                }

                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                _logger.LogInformation(
                    "OpeningBalance.Save completed for FiscalYearId {FiscalYearId}. Inserted {Inserted}, Updated {Updated}, JournalEntry {EntryNumber}, Debit {Debit}, Credit {Credit}, BalancingEquity {Equity}.",
                    model.FiscalYearId, inserted, updated, postedEntryNumber ?? "(none)", totalDebit, totalCredit, equityBalancingAmount);

                if (postedEntryNumber != null)
                {
                    var equityNote = equityBalancingAmount > 0
                        ? $" A balancing entry of ₦{equityBalancingAmount:N2} was posted to Opening Balance Equity (3102)."
                        : "";
                    TempData["Success"] =
                        $"Opening balances saved and posted as journal entry {postedEntryNumber} " +
                        $"(Total: ₦{totalDebit:N2} debit = ₦{totalCredit:N2} credit).{equityNote}";
                }
                else
                {
                    TempData["Success"] = "Opening balances saved. No journal entry was posted because all balances were zero.";
                }

                return RedirectToAction(nameof(Index), new { fiscalYearId = model.FiscalYearId });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                _logger.LogError(ex, "OpeningBalance.Save failed for FiscalYearId {FiscalYearId}.", model.FiscalYearId);

                ModelState.AddModelError(string.Empty,
                    "An error occurred while saving opening balances. The change has been rolled back. Please try again.");
                await PopulateFiscalYearDropdownAsync(model);
                return View("Index", model);
            }
        }

        private async Task PopulateFiscalYearDropdownAsync(OpeningBalanceFiscalYearViewModel model)
        {
            var fiscalYears = await _context.FiscalYears
                .OrderBy(fy => fy.StartDate)
                .ToListAsync();

            model.FiscalYears = fiscalYears.Select(fy => new SelectListItem
            {
                Value = fy.Id.ToString(),
                Text = fy.YearCode,
                Selected = fy.Id == model.FiscalYearId
            }).ToList();

            var fy = fiscalYears.FirstOrDefault(f => f.Id == model.FiscalYearId);
            if (fy != null)
            {
                model.FiscalYearName = $"{fy.YearCode} ({fy.StartDate:dd/MM/yyyy} - {fy.EndDate:dd/MM/yyyy})";
            }

            // Rehydrate account metadata (code/name/type/subtype) on the posted rows.
            // The POST payload only carries AccountId and OpeningBalance, so on an
            // error re-render the display columns would otherwise be blank.
            if (model.Accounts != null && model.Accounts.Any())
            {
                var accountIds = model.Accounts.Select(a => a.AccountId).ToList();
                var accounts = await _context.ChartOfAccounts
                    .Where(a => accountIds.Contains(a.Id))
                    .ToListAsync();
                var lookup = accounts.ToDictionary(a => a.Id, a => a);

                foreach (var row in model.Accounts)
                {
                    if (lookup.TryGetValue(row.AccountId, out var acct))
                    {
                        row.AccountCode = acct.AccountCode;
                        row.AccountName = acct.AccountName;
                        row.AccountType = acct.AccountType;
                        row.SubType = acct.SubType;
                    }
                }
            }
            else
            {
                // No rows came back at all — rebuild the full list from scratch
                // using the same logic as the GET Index action.
                var accounts = await _context.ChartOfAccounts
                    .OrderBy(a => a.AccountCode)
                    .ToListAsync();

                var balances = await _context.AccountFiscalYearBalances
                    .Where(b => b.FiscalYearId == model.FiscalYearId)
                    .ToListAsync();
                var balanceLookup = balances.ToDictionary(b => b.AccountId, b => b);

                model.Accounts = new List<OpeningBalanceAccountRow>();
                foreach (var account in accounts)
                {
                    balanceLookup.TryGetValue(account.Id, out var balance);
                    model.Accounts.Add(new OpeningBalanceAccountRow
                    {
                        AccountId = account.Id,
                        AccountCode = account.AccountCode,
                        AccountName = account.AccountName,
                        AccountType = account.AccountType,
                        SubType = account.SubType,
                        OpeningBalance = balance?.OpeningBalance ?? account.OpeningBalance
                    });
                }
            }
        }
    }
}
