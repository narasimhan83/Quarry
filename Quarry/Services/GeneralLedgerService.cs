using Microsoft.EntityFrameworkCore;
using QuarryManagementSystem.Data;
using QuarryManagementSystem.Models.Domain;

namespace QuarryManagementSystem.Services
{
    /// <summary>
    /// General Ledger posting service. Owns the journal-entry side effects
    /// for inventory-related events that don't have a "natural home" controller
    /// (i.e. events triggered from multiple controllers, or events that need
    /// to coordinate with InvoiceController without that controller knowing
    /// about them).
    /// <para/>
    /// Phase 4 scope:
    ///   * PostWeighmentSaleAsync — when a weighment is Completed, post the
    ///     full sale entry: Dr 1101 AR + Dr 4010 Rebate + Dr 5001 COGS,
    ///     Cr 4001 Sales + Cr 2101 VAT + Cr 1302 Finished Goods. Replaces the
    ///     AR/Revenue/VAT/COGS posting that previously happened only at
    ///     invoice creation time.
    ///   * ReverseWeighmentSaleAsync — when a Completed weighment is moved
    ///     out of Completed status, delete the WBS journal entry and
    ///     recompute affected balances. Mirrors the
    ///     ReverseInvoiceJournalEntryAsync pattern in InvoiceController.
    /// <para/>
    /// What this service does NOT do (deliberately):
    ///   * Does not call SaveChangesAsync after the journal post itself.
    ///     The caller controls the transaction so the GL post can be atomic
    ///     with the source-document save and the inventory hit.
    ///   * Does not inject HttpContext / IHttpContextAccessor. The userId is
    ///     passed in by the calling controller (which already has access via
    ///     ClaimTypes.NameIdentifier), keeping the service test-friendly.
    /// </summary>
    public interface IGeneralLedgerService
    {
        /// <summary>
        /// Posts the WBS journal entry for a Completed weighment. Returns true
        /// if the entry posted (or there was nothing to post — e.g. zero
        /// total weighment), false on validation failure or imbalance.
        /// <para/>
        /// The caller is responsible for ensuring this is invoked exactly once
        /// per Completed-transition. Calling it twice on the same weighment
        /// will produce a duplicate WBS entry; the service does not currently
        /// guard against that because the duplicate would be a controller bug,
        /// not a runtime input that needs handling.
        /// </summary>
        /// <param name="weighment">The weighment being completed. Must already
        /// have NetWeight, SubTotal, VatAmount, RebateAmount, TotalAmount
        /// populated by ApplyVatTreatmentAsync.</param>
        /// <param name="cogsAmount">Cost-of-goods-sold amount, computed as
        /// quantityInTons * weighted-average-unit-cost, returned by
        /// IInventoryService.RecordSaleAsync.</param>
        /// <param name="userId">AspNetUsers.Id (NameIdentifier claim), used for
        /// the JournalEntry.PostedBy FK. Pass null for unauthenticated callers
        /// (rare; mostly system-job paths).</param>
        Task<bool> PostWeighmentSaleAsync(WeighmentTransaction weighment, decimal cogsAmount, string? userId);

        /// <summary>
        /// Reverses the WBS journal entry for a weighment. Idempotent — if no
        /// WBS entry exists (e.g. the weighment was Completed before Phase 4
        /// went live, or the prior post failed), this is a no-op.
        /// <para/>
        /// Implementation matches ReverseInvoiceJournalEntryAsync in
        /// InvoiceController: deletes the entry rather than posting a
        /// negating one, then recomputes balances on every account that
        /// was touched.
        /// </summary>
        Task ReverseWeighmentSaleAsync(int weighmentId);

        /// <summary>
        /// Reads the integer Id of a ChartOfAccounts row by its account code.
        /// Returns 0 if the code does not exist. Mirrors the helper in
        /// InvoiceController so both sides resolve account codes the same way.
        /// </summary>
        Task<int> GetAccountIdByCodeAsync(string code);

        /// <summary>
        /// Recomputes <see cref="ChartOfAccounts.CurrentBalance"/> for the
        /// given account from its journal-entry-line history. Mirrors the
        /// helper in InvoiceController exactly so balances stay consistent
        /// regardless of which controller posts the entry.
        /// </summary>
        Task RecalculateAccountBalanceAsync(int accountId);

        /// <summary>
        /// Returns true if a WBS journal entry exists for the given
        /// weighment. Used by InvoiceController to detect "post-Phase-4"
        /// weighments and skip the AR/Revenue/VAT lines that would otherwise
        /// double-post when invoicing them.
        /// </summary>
        Task<bool> HasWeighmentSalePostedAsync(string transactionNumber);
    }

    public class GeneralLedgerService : IGeneralLedgerService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<GeneralLedgerService> _logger;

        // Account codes used by Phase 4. Centralised here so a future Chart
        // of Accounts rename (e.g. 4001 → 4011) is a one-line change.
        private const string AR_CODE        = "1101"; // Accounts Receivable
        private const string FG_CODE        = "1302"; // Finished Goods Inventory
        private const string VAT_OUTPUT     = "2101"; // VAT Output Tax
        private const string SALES_CODE     = "4001"; // Sale of Aggregates
        private const string REBATE_CODE    = "4010"; // Sales Rebates & Discounts
        private const string COGS_CODE      = "5001"; // Cost of Goods Sold

        // WBS = Weighment-Based Sale. Reserved entry-number prefix so we can
        // distinguish these journals from INV (invoice), PAY (payment),
        // ADVAPPLY (prepayment application), etc. when querying or reversing.
        public const string WBS_PREFIX = "WBS";

        public GeneralLedgerService(
            ApplicationDbContext context,
            ILogger<GeneralLedgerService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<bool> PostWeighmentSaleAsync(WeighmentTransaction weighment, decimal cogsAmount, string? userId)
        {
            try
            {
                // Zero-amount weighments don't need a ledger entry. Defensive
                // — Phase 3 already refuses to Complete weighments with zero
                // net weight, so this is a belt-and-braces guard rather than
                // an expected path.
                if ((weighment.TotalAmount ?? 0) <= 0 && cogsAmount <= 0)
                {
                    return true;
                }

                // Resolve the customer's VAT type the same way InvoiceController
                // does — the weighment doesn't carry a VatTypeSnapshot field, so
                // we look up the customer fresh. If no customer is set (very
                // unusual for a Completed weighment), default to Exclusive
                // since that matches the historical default behavior.
                string vatType = "Exclusive";
                if (weighment.CustomerId.HasValue)
                {
                    var lookup = await _context.Customers
                        .Include(c => c.VatType)
                        .Where(c => c.Id == weighment.CustomerId.Value)
                        .Select(c => c.VatType != null ? c.VatType.Name : null)
                        .FirstOrDefaultAsync();
                    if (!string.IsNullOrWhiteSpace(lookup))
                    {
                        vatType = lookup!;
                    }
                }

                // Mirror the math in InvoiceController.CreateInvoiceJournalEntryAsync
                // but at the weighment level. The weighment carries SubTotal,
                // VatAmount, RebateAmount, and TotalAmount produced by
                // ApplyVatTreatmentAsync. We do NOT include transport here —
                // transport is customer-level, applied per-invoice, and posts
                // separately when the invoice is cut.
                var subTotalRaw   = weighment.SubTotal     ?? 0m;
                var rebateRaw     = weighment.RebateAmount ?? 0m;
                var vatAmount     = weighment.VatAmount    ?? 0m;
                var arDebit       = weighment.TotalAmount  ?? 0m;

                decimal salesCredit;
                decimal rebateDebit;

                var isInclusive = vatType.IndexOf("Inclusive", StringComparison.OrdinalIgnoreCase) >= 0;
                if (isInclusive)
                {
                    // Inclusive: SubTotal is gross (includes VAT). Back the
                    // VAT share out of it so 4001 Sales is net.
                    //
                    // Apportion VAT between sales and rebate by their
                    // respective shares of the gross base (SubTotal − Rebate).
                    // No transport here — that's the invoice's job.
                    var grossBase = subTotalRaw - rebateRaw;
                    if (grossBase <= 0)
                    {
                        _logger.LogWarning(
                            "Weighment {TransactionNumber}: zero/negative gross base; skipping ledger post.",
                            weighment.TransactionNumber);
                        return false;
                    }

                    var vatShareOfRebate = rebateRaw > 0
                        ? Math.Round(vatAmount * (rebateRaw / subTotalRaw), 2)
                        : 0m;
                    var vatShareOfSales = vatAmount - vatShareOfRebate;

                    salesCredit = Math.Round(subTotalRaw - vatShareOfSales - rebateRaw, 2);
                    rebateDebit = rebateRaw > 0
                        ? Math.Round(rebateRaw - vatShareOfRebate, 2)
                        : 0m;
                }
                else
                {
                    // Exclusive: SubTotal is already net of VAT, post directly.
                    // Rebate is a separate Dr line at face value.
                    salesCredit = subTotalRaw;
                    rebateDebit = rebateRaw;
                }

                // Pre-balance check on the revenue side: Dr AR + Dr Rebate
                // should equal Cr Sales + Cr VAT (excluding the COGS pair,
                // which balances on its own).
                var revDr = arDebit + rebateDebit;
                var revCr = salesCredit + vatAmount;
                if (Math.Abs(revDr - revCr) > 0.02m)
                {
                    _logger.LogError(
                        "Weighment {TransactionNumber} revenue side won't balance: Dr={D} Cr={C}. Skipping.",
                        weighment.TransactionNumber, revDr, revCr);
                    return false;
                }
                // Absorb up-to-2-kobo rounding residual into the sales credit
                // so the entry is exact. Same trick InvoiceController uses.
                if (revDr != revCr)
                {
                    salesCredit += (revDr - revCr);
                }

                // Resolve all account ids up-front so we can pre-flight-check
                // every code we're about to touch. A missing code returns 0
                // and would blow up the FK on insert — better to log and
                // refuse cleanly here.
                var arId      = await GetAccountIdByCodeAsync(AR_CODE);
                var salesId   = await GetAccountIdByCodeAsync(SALES_CODE);
                var vatId     = await GetAccountIdByCodeAsync(VAT_OUTPUT);
                var rebateId  = await GetAccountIdByCodeAsync(REBATE_CODE);
                var cogsId    = await GetAccountIdByCodeAsync(COGS_CODE);
                var fgId      = await GetAccountIdByCodeAsync(FG_CODE);

                if (arId == 0 || salesId == 0 || cogsId == 0 || fgId == 0
                    || (rebateDebit > 0 && rebateId == 0)
                    || (vatAmount   > 0 && vatId    == 0))
                {
                    _logger.LogError(
                        "Weighment {TransactionNumber}: required ledger account(s) missing " +
                        "(1101={AR}, 4001={Sales}, 4010={Rebate}, 2101={VAT}, 5001={COGS}, 1302={FG}). " +
                        "Run the inventory phase-1 SQL migration if you haven't yet.",
                        weighment.TransactionNumber, arId, salesId, rebateId, vatId, cogsId, fgId);
                    return false;
                }

                var entry = new JournalEntry
                {
                    EntryNumber = JournalEntry.GenerateEntryNumber(WBS_PREFIX),
                    EntryDate = weighment.TransactionDate,
                    // Reference is the weighment's transaction number — that's
                    // what InvoiceController will look for to detect "this
                    // weighment already has its own GL entry."
                    Reference = weighment.TransactionNumber,
                    Description = $"Weighbridge sale {weighment.TransactionNumber} for customer {weighment.CustomerId}",
                    PostedBy = userId,
                    IsAutoGenerated = true,
                    CreatedAt = DateTime.Now
                };

                // Dr Accounts Receivable
                if (arDebit > 0)
                {
                    entry.JournalEntryLines.Add(new JournalEntryLine
                    {
                        AccountId = arId,
                        DebitAmount = arDebit,
                        CreditAmount = 0,
                        LineDescription = $"AR raised for weighment {weighment.TransactionNumber}"
                    });
                }

                // Dr Sales Rebates & Discounts (contra-revenue) — only when rebate > 0
                if (rebateDebit > 0)
                {
                    entry.JournalEntryLines.Add(new JournalEntryLine
                    {
                        AccountId = rebateId,
                        DebitAmount = rebateDebit,
                        CreditAmount = 0,
                        LineDescription = $"Customer rebate on weighment {weighment.TransactionNumber}"
                    });
                }

                // Cr Sale of Aggregates
                if (salesCredit > 0)
                {
                    entry.JournalEntryLines.Add(new JournalEntryLine
                    {
                        AccountId = salesId,
                        DebitAmount = 0,
                        CreditAmount = salesCredit,
                        LineDescription = $"Sales revenue for weighment {weighment.TransactionNumber}"
                    });
                }

                // Cr VAT Output Tax — only when VAT > 0
                if (vatAmount > 0)
                {
                    entry.JournalEntryLines.Add(new JournalEntryLine
                    {
                        AccountId = vatId,
                        DebitAmount = 0,
                        CreditAmount = vatAmount,
                        LineDescription = $"VAT output on weighment {weighment.TransactionNumber}"
                    });
                }

                // Dr Cost of Goods Sold + Cr Finished Goods Inventory
                // The COGS pair is an independent balanced sub-entry — it
                // matches the inventory drawn down by IInventoryService.
                // RecordSaleAsync, posted at the WAC at sale time.
                if (cogsAmount > 0)
                {
                    entry.JournalEntryLines.Add(new JournalEntryLine
                    {
                        AccountId = cogsId,
                        DebitAmount = cogsAmount,
                        CreditAmount = 0,
                        LineDescription = $"COGS for weighment {weighment.TransactionNumber}"
                    });
                    entry.JournalEntryLines.Add(new JournalEntryLine
                    {
                        AccountId = fgId,
                        DebitAmount = 0,
                        CreditAmount = cogsAmount,
                        LineDescription = $"Finished goods drawn for weighment {weighment.TransactionNumber}"
                    });
                }

                entry.RecalculateTotals();

                // Final defensive check before insert. If this fails, the
                // weighment-side and COGS-side math somehow disagreed.
                if (!entry.IsBalanced())
                {
                    _logger.LogError(
                        "Weighment {TransactionNumber}: built journal not balanced (Dr={D} Cr={C}). Aborting post.",
                        weighment.TransactionNumber, entry.TotalDebit, entry.TotalCredit);
                    return false;
                }

                _context.JournalEntries.Add(entry);
                await _context.SaveChangesAsync();

                // Refresh running balances on every account we touched.
                // RecalculateAccountBalanceAsync does its own SaveChanges.
                if (arDebit > 0)     await RecalculateAccountBalanceAsync(arId);
                if (rebateDebit > 0) await RecalculateAccountBalanceAsync(rebateId);
                if (salesCredit > 0) await RecalculateAccountBalanceAsync(salesId);
                if (vatAmount > 0)   await RecalculateAccountBalanceAsync(vatId);
                if (cogsAmount > 0)
                {
                    await RecalculateAccountBalanceAsync(cogsId);
                    await RecalculateAccountBalanceAsync(fgId);
                }
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Posted WBS journal {EntryNumber} for weighment {TransactionNumber}: Dr={D:N2} Cr={C:N2}",
                    entry.EntryNumber, weighment.TransactionNumber, entry.TotalDebit, entry.TotalCredit);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error posting WBS journal for weighment {TransactionNumber}",
                    weighment?.TransactionNumber);
                return false;
            }
        }

        public async Task ReverseWeighmentSaleAsync(int weighmentId)
        {
            try
            {
                // Look up the weighment's transaction number — that's what
                // we used as the Reference when we posted the WBS entry.
                var transactionNumber = await _context.WeighmentTransactions
                    .Where(w => w.Id == weighmentId)
                    .Select(w => w.TransactionNumber)
                    .FirstOrDefaultAsync();

                if (string.IsNullOrEmpty(transactionNumber))
                {
                    // Weighment was deleted before we got here; nothing to do.
                    return;
                }

                var prior = await _context.JournalEntries
                    .Include(je => je.JournalEntryLines)
                    .Where(je =>
                        je.EntryNumber.StartsWith(WBS_PREFIX) &&
                        je.Reference == transactionNumber)
                    .ToListAsync();

                if (prior.Count == 0)
                {
                    // No WBS entry exists. This is normal in two cases:
                    //   * The weighment was Completed before Phase 4 went live
                    //     (its GL entry is on the invoice side, not here).
                    //   * The original post failed and the controller saved
                    //     the row anyway. Nothing to undo either way.
                    return;
                }

                // Capture affected accounts before deletion so we can recompute
                // their running balances after the rows are gone.
                var affectedAccountIds = prior
                    .SelectMany(je => je.JournalEntryLines.Select(l => l.AccountId))
                    .Distinct()
                    .ToList();

                foreach (var je in prior)
                {
                    _context.JournalEntryLines.RemoveRange(je.JournalEntryLines);
                }
                _context.JournalEntries.RemoveRange(prior);
                await _context.SaveChangesAsync();

                foreach (var accountId in affectedAccountIds)
                {
                    await RecalculateAccountBalanceAsync(accountId);
                }
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Reversed WBS journal(s) for weighment {TransactionNumber}: {Count} entry(ies) removed",
                    transactionNumber, prior.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error reversing WBS journal for weighment {WeighmentId}",
                    weighmentId);
                throw; // Surface to the caller so the outer transaction rolls back.
            }
        }

        public async Task<int> GetAccountIdByCodeAsync(string code)
        {
            var acc = await _context.ChartOfAccounts
                .FirstOrDefaultAsync(a => a.AccountCode == code);
            return acc?.Id ?? 0;
        }

        public async Task RecalculateAccountBalanceAsync(int accountId)
        {
            var account = await _context.ChartOfAccounts.FirstOrDefaultAsync(a => a.Id == accountId);
            if (account == null) return;

            var totals = await _context.JournalEntryLines
                .Where(l => l.AccountId == accountId)
                .GroupBy(l => l.AccountId)
                .Select(g => new
                {
                    Debit  = g.Sum(l => l.DebitAmount),
                    Credit = g.Sum(l => l.CreditAmount)
                })
                .FirstOrDefaultAsync();

            decimal totalDebit  = totals?.Debit  ?? 0m;
            decimal totalCredit = totals?.Credit ?? 0m;

            // Asset/Expense accounts: Dr increases, Cr decreases. Net = Dr − Cr.
            // Liability/Equity/Revenue accounts: opposite. Net = Cr − Dr.
            // Same convention used by InvoiceController.
            decimal netMovement = (account.IsAssetAccount() || account.IsExpenseAccount())
                ? totalDebit - totalCredit
                : totalCredit - totalDebit;

            account.CurrentBalance = account.OpeningBalance + netMovement;
        }

        public async Task<bool> HasWeighmentSalePostedAsync(string transactionNumber)
        {
            if (string.IsNullOrEmpty(transactionNumber)) return false;
            return await _context.JournalEntries
                .AnyAsync(je =>
                    je.EntryNumber.StartsWith(WBS_PREFIX) &&
                    je.Reference == transactionNumber);
        }
    }
}
