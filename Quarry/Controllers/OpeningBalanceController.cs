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
    public class OpeningBalanceController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<OpeningBalanceController> _logger;

        public OpeningBalanceController(ApplicationDbContext context, ILogger<OpeningBalanceController> logger)
        {
            _context = context;
            _logger = logger;
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

                // Default to the global OpeningBalance when there is no per-year record yet
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
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(OpeningBalanceFiscalYearViewModel model)
        {
            // Basic diagnostics so we can see what's happening in the logs
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

            var accountIds = (model.Accounts ?? new List<OpeningBalanceAccountRow>())
                .Select(a => a.AccountId)
                .ToList();

            _logger.LogInformation("OpeningBalance.Save: processing {AccountIdCount} distinct AccountIds.", accountIds.Count);

            var existingBalances = await _context.AccountFiscalYearBalances
                .Where(b => b.FiscalYearId == model.FiscalYearId && accountIds.Contains(b.AccountId))
                .ToListAsync();

            var existingLookup = existingBalances.ToDictionary(b => b.AccountId, b => b);

            int updated = 0;
            int inserted = 0;

            foreach (var row in model.Accounts)
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

            var changes = await _context.SaveChangesAsync();

            _logger.LogInformation(
                "OpeningBalance.Save completed for FiscalYearId {FiscalYearId}. Inserted {Inserted}, Updated {Updated}, SaveChanges affected {Changes}.",
                model.FiscalYearId,
                inserted,
                updated,
                changes);

            TempData["Success"] = "Opening balances saved successfully.";
            return RedirectToAction(nameof(Index), new { fiscalYearId = model.FiscalYearId });
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
        }
    }
}