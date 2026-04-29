-- =========================================================================
-- Quarry Management System — Seed the 4010 "Sales Rebates & Discounts"
-- contra-revenue account.
--
-- Why: InvoiceController.CreateInvoiceJournalEntryAsync tries to post a
-- Dr to account code 4010 whenever an invoice carries a rebate. If that
-- account doesn't exist, GetAccountIdByCodeAsync returns 0, the insert
-- violates the JournalEntryLines → ChartOfAccounts FK, the exception is
-- caught-and-swallowed by the helper, and the whole invoice journal entry
-- is silently dropped. This script adds the account so new invoices post
-- correctly.
--
-- Idempotent: only inserts when the row doesn't already exist.
-- =========================================================================

------------------------------------------------------------------------
-- 1. Insert the account if missing. Id 23 matches the seed in
--    ApplicationDbContext.OnModelCreating so the two paths agree.
------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM dbo.ChartOfAccounts WHERE AccountCode = '4010')
BEGIN
    -- Check if Id 23 is free; if not, let SQL pick a new Id to avoid PK clash.
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

PRINT 'Account 4010 (Sales Rebates & Discounts) ensured.';
GO
