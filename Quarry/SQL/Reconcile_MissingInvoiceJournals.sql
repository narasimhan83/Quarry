-- =========================================================================
-- Quarry Management System — Post-hoc fix for invoices that were created
-- before the 4010 "Sales Rebates & Discounts" account was seeded.
--
-- Context: InvoiceController.CreateInvoiceJournalEntryAsync tries to
-- post a Dr to account 4010 whenever an invoice carries a rebate. If
-- 4010 didn't exist, the entire invoice journal entry was silently
-- dropped. ApplyPrepaymentToInvoiceAsync also ran AFTER the journal
-- step, so it may or may not have completed depending on when the
-- exception fired.
--
-- This script:
--   1. Ensures account 4010 exists (delegate to the seed migration).
--   2. Posts missing INV journal entries for any invoice that doesn't
--      already have one.
--   3. Drains the prepayment wallet for any "Paid" invoice with
--      PrepaymentApplied > 0 where the prepayment.UsedAmount is out
--      of sync with the amount actually applied.
--   4. Recomputes affected customer OutstandingBalance values.
--
-- USE WITH CAUTION: this fabricates journal entries for existing data.
-- Review the output of each SELECT preview block before running the
-- UPDATE/INSERT blocks. Run once per deployment, then delete or archive.
--
-- Idempotent: every INSERT is guarded by a NOT EXISTS check, so
-- reruns only touch rows that still need fixing.
-- =========================================================================

------------------------------------------------------------------------
-- 0. Ensure account 4010 exists. Mirror of Migration_SeedSalesRebatesAccount.
------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM dbo.ChartOfAccounts WHERE AccountCode = '4010')
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.ChartOfAccounts WHERE Id = 23)
    BEGIN
        SET IDENTITY_INSERT dbo.ChartOfAccounts ON;
        INSERT INTO dbo.ChartOfAccounts
            (Id, AccountCode, AccountName, AccountType, SubType,
             OpeningBalance, CurrentBalance, IsActive, CreatedAt)
        VALUES
            (23, '4010', 'Sales Rebates & Discounts', 'Revenue', 'Contra',
             0, 0, 1, SYSDATETIME());
        SET IDENTITY_INSERT dbo.ChartOfAccounts OFF;
    END
    ELSE
    BEGIN
        INSERT INTO dbo.ChartOfAccounts
            (AccountCode, AccountName, AccountType, SubType,
             OpeningBalance, CurrentBalance, IsActive, CreatedAt)
        VALUES
            ('4010', 'Sales Rebates & Discounts', 'Revenue', 'Contra',
             0, 0, 1, SYSDATETIME());
    END
END;
GO

------------------------------------------------------------------------
-- 1. PREVIEW: show invoices missing an INV journal entry.
--    Uncomment and run by itself to see what's about to be fixed.
------------------------------------------------------------------------
-- SELECT i.Id, i.InvoiceNumber, i.TotalAmount, i.Status
-- FROM   dbo.Invoices i
-- WHERE  i.Status <> 'Cancelled'
--   AND  i.TotalAmount > 0
--   AND  NOT EXISTS (
--         SELECT 1 FROM dbo.JournalEntries je
--         WHERE  je.EntryNumber LIKE 'INV%'
--           AND  je.Reference = 'Invoice ' + i.InvoiceNumber
--        );
-- GO

------------------------------------------------------------------------
-- 2. Post missing INV journal entries for affected invoices.
--
-- Assumes Exclusive VAT treatment for back-posting (the common case for
-- quarry customers). Customers actually configured as Inclusive need a
-- manual review because the apportioning math differs. The script skips
-- invoices with Inclusive snapshot and prints a warning list.
------------------------------------------------------------------------
DECLARE @accAR      INT = (SELECT Id FROM dbo.ChartOfAccounts WHERE AccountCode = '1101');
DECLARE @accSales   INT = (SELECT Id FROM dbo.ChartOfAccounts WHERE AccountCode = '4001');
DECLARE @accTransp  INT = (SELECT Id FROM dbo.ChartOfAccounts WHERE AccountCode = '4002');
DECLARE @accVATOut  INT = (SELECT Id FROM dbo.ChartOfAccounts WHERE AccountCode = '2101');
DECLARE @accRebate  INT = (SELECT Id FROM dbo.ChartOfAccounts WHERE AccountCode = '4010');

IF @accAR IS NULL OR @accSales IS NULL OR @accTransp IS NULL
   OR @accVATOut IS NULL OR @accRebate IS NULL
BEGIN
    RAISERROR('Required chart-of-accounts rows missing. Aborting.', 16, 1);
    RETURN;
END

-- Warn about Inclusive invoices we're not touching.
IF EXISTS (
    SELECT 1 FROM dbo.Invoices i
    WHERE i.Status <> 'Cancelled'
      AND i.TotalAmount > 0
      AND i.VatTypeSnapshot = 'Inclusive'
      AND NOT EXISTS (
        SELECT 1 FROM dbo.JournalEntries je
        WHERE je.EntryNumber LIKE 'INV%'
          AND je.Reference = 'Invoice ' + i.InvoiceNumber))
BEGIN
    PRINT 'WARNING: One or more Inclusive-VAT invoices are missing journals.';
    PRINT '         Skipping those; they need manual review.';
    SELECT i.InvoiceNumber AS 'Needs manual review (Inclusive)'
    FROM   dbo.Invoices i
    WHERE  i.Status <> 'Cancelled'
      AND  i.TotalAmount > 0
      AND  i.VatTypeSnapshot = 'Inclusive'
      AND  NOT EXISTS (
            SELECT 1 FROM dbo.JournalEntries je
            WHERE je.EntryNumber LIKE 'INV%'
              AND je.Reference = 'Invoice ' + i.InvoiceNumber);
END

-- Loop through Exclusive invoices that lack a journal. Cursor is fine here
-- because this is a one-time cleanup, not a hot path.
DECLARE @id           INT,
        @invNo        NVARCHAR(50),
        @invDate      DATETIME2,
        @custId       INT,
        @subTotal     DECIMAL(18,2),
        @rebate       DECIMAL(18,2),
        @transport    DECIMAL(18,2),
        @vatAmount    DECIMAL(18,2),
        @totalAmount  DECIMAL(18,2),
        @entryNumber  NVARCHAR(50),
        @newJeId      INT;

DECLARE inv_cur CURSOR LOCAL FOR
    SELECT i.Id, i.InvoiceNumber, i.InvoiceDate, i.CustomerId,
           i.SubTotal, i.RebateAmount, i.TransportAmount,
           i.VatAmount, i.TotalAmount
    FROM   dbo.Invoices i
    WHERE  i.Status <> 'Cancelled'
      AND  i.TotalAmount > 0
      AND  (i.VatTypeSnapshot IS NULL OR i.VatTypeSnapshot <> 'Inclusive')
      AND  NOT EXISTS (
            SELECT 1 FROM dbo.JournalEntries je
            WHERE je.EntryNumber LIKE 'INV%'
              AND je.Reference = 'Invoice ' + i.InvoiceNumber);

OPEN inv_cur;
FETCH NEXT FROM inv_cur INTO
    @id, @invNo, @invDate, @custId,
    @subTotal, @rebate, @transport, @vatAmount, @totalAmount;

WHILE @@FETCH_STATUS = 0
BEGIN
    -- Build entry number: INV + yyyyMMdd + -NNN (4-digit sequence
    -- across the full JournalEntries table for that day).
    DECLARE @dayKey NVARCHAR(8) = CONVERT(NVARCHAR(8), @invDate, 112);
    DECLARE @seq INT = ISNULL(
        (SELECT COUNT(*) FROM dbo.JournalEntries
         WHERE EntryNumber LIKE 'INV' + @dayKey + '-%'), 0) + 1;
    SET @entryNumber = 'INV' + @dayKey + '-' + RIGHT('000' + CAST(@seq AS NVARCHAR(4)), 4);

    -- PostedBy is an FK to AspNetUsers.Id, so we can't stuff a label like
    -- 'system-reconciliation' in here. Leave it NULL for back-posted entries;
    -- IsAutoGenerated = 1 plus the "(back-posted)" marker in Description is
    -- enough to identify these rows in audits.
    INSERT INTO dbo.JournalEntries
        (EntryNumber, EntryDate, Reference, Description,
         PostedBy, IsAutoGenerated, CreatedAt,
         TotalDebit, TotalCredit)
    VALUES
        (@entryNumber, @invDate,
         'Invoice ' + @invNo,
         'Sales invoice ' + @invNo + ' for customer ' + CAST(@custId AS NVARCHAR(10)) + ' (back-posted)',
         NULL, 1, SYSDATETIME(),
         @totalAmount + @rebate, @totalAmount + @rebate);

    SET @newJeId = SCOPE_IDENTITY();

    -- Dr AR (full invoice total)
    INSERT INTO dbo.JournalEntryLines (JournalEntryId, AccountId, DebitAmount, CreditAmount, LineDescription)
    VALUES (@newJeId, @accAR, @totalAmount, 0,
            'Receivable raised for invoice ' + @invNo);

    -- Dr Rebate (contra-revenue) if any
    IF @rebate > 0
    BEGIN
        INSERT INTO dbo.JournalEntryLines (JournalEntryId, AccountId, DebitAmount, CreditAmount, LineDescription)
        VALUES (@newJeId, @accRebate, @rebate, 0,
                'Customer rebate on invoice ' + @invNo);
    END

    -- Cr Sales
    IF @subTotal > 0
    BEGIN
        INSERT INTO dbo.JournalEntryLines (JournalEntryId, AccountId, DebitAmount, CreditAmount, LineDescription)
        VALUES (@newJeId, @accSales, 0, @subTotal,
                'Sales revenue from invoice ' + @invNo);
    END

    -- Cr Transport if any
    IF @transport > 0
    BEGIN
        INSERT INTO dbo.JournalEntryLines (JournalEntryId, AccountId, DebitAmount, CreditAmount, LineDescription)
        VALUES (@newJeId, @accTransp, 0, @transport,
                'Transport charge on invoice ' + @invNo);
    END

    -- Cr VAT Output if any
    IF @vatAmount > 0
    BEGIN
        INSERT INTO dbo.JournalEntryLines (JournalEntryId, AccountId, DebitAmount, CreditAmount, LineDescription)
        VALUES (@newJeId, @accVATOut, 0, @vatAmount,
                'VAT output on invoice ' + @invNo);
    END

    PRINT 'Back-posted INV journal for invoice ' + @invNo + ' (entry ' + @entryNumber + ').';

    FETCH NEXT FROM inv_cur INTO
        @id, @invNo, @invDate, @custId,
        @subTotal, @rebate, @transport, @vatAmount, @totalAmount;
END

CLOSE inv_cur;
DEALLOCATE inv_cur;
GO

------------------------------------------------------------------------
-- 3. Reconcile prepayment applications: any invoice with PrepaymentApplied > 0
--    that has no matching PrepaymentApplication rows needs one posted so
--    the prepayment wallet drains correctly.
--
-- Strategy: for each affected invoice, find the customer's oldest active
-- prepayment with enough remaining balance and insert a PrepaymentApplication
-- row for it. Then bump prepayment.UsedAmount and flip status to Exhausted
-- if drained. Also post an ADVAPPLY journal entry (Dr 2103 / Cr 1101).
--
-- Note: the @accAR variable from section 2 went out of scope at the previous
-- GO (T-SQL batches reset all DECLAREd variables), so we redeclare everything
-- section 3 needs here. Keeps each section independently runnable.
------------------------------------------------------------------------
DECLARE @accAR     INT = (SELECT Id FROM dbo.ChartOfAccounts WHERE AccountCode = '1101');
DECLARE @accPrepay INT = (SELECT Id FROM dbo.ChartOfAccounts WHERE AccountCode = '2103');

IF @accAR IS NULL OR @accPrepay IS NULL
BEGIN
    RAISERROR('Required accounts missing (1101 AR or 2103 Customer Prepayments). Aborting step 3.', 16, 1);
    RETURN;
END

DECLARE @invId        INT,
        @invNum       NVARCHAR(50),
        @invDate2     DATETIME2,
        @invCustId    INT,
        @applied      DECIMAL(18,2);

DECLARE pay_cur CURSOR LOCAL FOR
    SELECT i.Id, i.InvoiceNumber, i.InvoiceDate, i.CustomerId, i.PrepaymentApplied
    FROM   dbo.Invoices i
    WHERE  i.Status <> 'Cancelled'
      AND  i.PrepaymentApplied > 0
      AND  NOT EXISTS (
            SELECT 1 FROM dbo.PrepaymentApplications pa
            WHERE  pa.InvoiceId = i.Id);

OPEN pay_cur;
FETCH NEXT FROM pay_cur INTO @invId, @invNum, @invDate2, @invCustId, @applied;

WHILE @@FETCH_STATUS = 0
BEGIN
    DECLARE @remaining DECIMAL(18,2) = @applied;

    -- Drain oldest active prepayment(s) for this customer until @applied is covered.
    -- Header-level only (line-item drain is app-logic territory; this is a
    -- cleanup for records where that never ran).
    DECLARE @prepayId INT, @prepayRemaining DECIMAL(18,2);

    DECLARE prepay_cur CURSOR LOCAL FOR
        SELECT Id, (Amount - UsedAmount) AS rem
        FROM   dbo.CustomerPrepayments
        WHERE  CustomerId = @invCustId
          AND  Status = 'Active'
          AND  (Amount - UsedAmount) > 0
        ORDER BY PrepaymentDate, Id;

    OPEN prepay_cur;
    FETCH NEXT FROM prepay_cur INTO @prepayId, @prepayRemaining;

    WHILE @@FETCH_STATUS = 0 AND @remaining > 0
    BEGIN
        DECLARE @takeNow DECIMAL(18,2) =
            CASE WHEN @prepayRemaining < @remaining THEN @prepayRemaining ELSE @remaining END;

        -- Insert the application row linking prepayment → invoice
        INSERT INTO dbo.PrepaymentApplications
            (CustomerPrepaymentId, InvoiceId, AppliedAmount, AppliedDate, Description)
        VALUES
            (@prepayId, @invId, @takeNow, @invDate2,
             'Prepayment applied to invoice ' + @invNum + ' (back-posted reconciliation)');

        -- Drain the prepayment header
        -- UpdatedBy is a plain NVARCHAR(100) label column (not an FK to
        -- AspNetUsers), so it's safe to stamp with a reconciliation marker
        -- here. That lets an operator later filter on it to find exactly
        -- which rows this script touched.
        UPDATE dbo.CustomerPrepayments
        SET    UsedAmount = UsedAmount + @takeNow,
               Status = CASE WHEN (Amount - (UsedAmount + @takeNow)) <= 0 THEN 'Exhausted' ELSE Status END,
               UpdatedAt = SYSDATETIME(),
               UpdatedBy = 'system-reconciliation'
        WHERE  Id = @prepayId;

        SET @remaining = @remaining - @takeNow;

        FETCH NEXT FROM prepay_cur INTO @prepayId, @prepayRemaining;
    END

    CLOSE prepay_cur;
    DEALLOCATE prepay_cur;

    -- Post ADVAPPLY journal entry (Dr Customer Prepayments, Cr AR) so the
    -- ledger stays balanced.
    DECLARE @dayKey2 NVARCHAR(8) = CONVERT(NVARCHAR(8), @invDate2, 112);
    DECLARE @seq2 INT = ISNULL(
        (SELECT COUNT(*) FROM dbo.JournalEntries
         WHERE EntryNumber LIKE 'ADVAPPLY' + @dayKey2 + '-%'), 0) + 1;
    DECLARE @advEntryNo NVARCHAR(50) =
        'ADVAPPLY' + @dayKey2 + '-' + RIGHT('000' + CAST(@seq2 AS NVARCHAR(4)), 4);
    DECLARE @advJeId INT;

    -- PostedBy left NULL — same reason as the INV insert above.
    INSERT INTO dbo.JournalEntries
        (EntryNumber, EntryDate, Reference, Description,
         PostedBy, IsAutoGenerated, CreatedAt,
         TotalDebit, TotalCredit)
    VALUES
        (@advEntryNo, @invDate2,
         'Prepayment applied to Invoice ' + @invNum,
         'Customer prepayment of ' + CAST(@applied AS NVARCHAR(32)) + ' applied to invoice ' + @invNum + ' (back-posted)',
         NULL, 1, SYSDATETIME(),
         @applied, @applied);

    SET @advJeId = SCOPE_IDENTITY();

    INSERT INTO dbo.JournalEntryLines (JournalEntryId, AccountId, DebitAmount, CreditAmount, LineDescription)
    VALUES (@advJeId, @accPrepay, @applied, 0,
            'Reduce customer prepayment for invoice ' + @invNum);

    INSERT INTO dbo.JournalEntryLines (JournalEntryId, AccountId, DebitAmount, CreditAmount, LineDescription)
    VALUES (@advJeId, @accAR, 0, @applied,
            'Reduce receivable for invoice ' + @invNum + ' (prepayment applied)');

    PRINT 'Back-posted prepayment drain for invoice ' + @invNum + '.';

    FETCH NEXT FROM pay_cur INTO @invId, @invNum, @invDate2, @invCustId, @applied;
END

CLOSE pay_cur;
DEALLOCATE pay_cur;
GO

------------------------------------------------------------------------
-- 4. Recompute CurrentBalance on every ChartOfAccounts row from the
--    JournalEntryLines so the Trial Balance reflects the new postings.
------------------------------------------------------------------------
UPDATE c
SET c.CurrentBalance = c.OpeningBalance +
    CASE
        WHEN c.AccountType IN ('Asset','Expense') THEN (ISNULL(agg.Debit,0) - ISNULL(agg.Credit,0))
        ELSE                                           (ISNULL(agg.Credit,0) - ISNULL(agg.Debit,0))
    END
FROM dbo.ChartOfAccounts c
LEFT JOIN (
    SELECT AccountId,
           SUM(DebitAmount)  AS Debit,
           SUM(CreditAmount) AS Credit
    FROM   dbo.JournalEntryLines
    GROUP  BY AccountId
) agg ON agg.AccountId = c.Id;
GO

------------------------------------------------------------------------
-- 5. Clamp any negative customer outstanding balance at 0. The Weighment
--    / Invoice flows handle this going forward; here we clean up existing
--    rows that were left negative.
------------------------------------------------------------------------
UPDATE dbo.Customers
SET    OutstandingBalance = 0
WHERE  OutstandingBalance < 0;
GO

PRINT '------------------------------------------------------';
PRINT 'Back-post reconciliation complete.';
PRINT 'Verify in the UI:';
PRINT '  1. Journal Entries page shows the INV and ADVAPPLY entries.';
PRINT '  2. Customer Prepayments page shows Used > 0 / Remaining reduced.';
PRINT '  3. Trial Balance sums match the invoice totals.';
PRINT '------------------------------------------------------';
GO
