-- =========================================================================
-- Quarry Management System - PrepaymentLineItems: VAT & Rebate audit columns
--
-- Adds two audit columns to PrepaymentLineItems so the Edit view and receipt
-- can always show "VAT N X included" and "Rebate N X applied" for each line,
-- even after the customer's VAT type or rebate configuration changes.
--
-- Idempotent: safe to run multiple times.
-- =========================================================================

IF COL_LENGTH('dbo.PrepaymentLineItems', 'VatAmount') IS NULL
BEGIN
    ALTER TABLE dbo.PrepaymentLineItems
        ADD VatAmount DECIMAL(18,2) NOT NULL
            CONSTRAINT DF_PrepaymentLineItems_VatAmount DEFAULT 0;
END;
GO

IF COL_LENGTH('dbo.PrepaymentLineItems', 'RebateAmount') IS NULL
BEGIN
    ALTER TABLE dbo.PrepaymentLineItems
        ADD RebateAmount DECIMAL(18,2) NOT NULL
            CONSTRAINT DF_PrepaymentLineItems_RebateAmount DEFAULT 0;
END;
GO

PRINT 'PrepaymentLineItems VAT / Rebate breakdown columns added.';
GO
