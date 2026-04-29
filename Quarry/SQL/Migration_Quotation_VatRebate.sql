-- =========================================================================
-- Quarry Management System - Quotation: VAT / Rebate / Transport audit columns
--
-- Mirrors the Invoice header-level pattern (RebateAmount/TransportAmount/
-- VatTypeSnapshot) and the PrepaymentLineItem per-line pattern (LineRebateAmount).
--
-- Header columns (Quotations):
--   RebateAmount        : flat customer rebate auto-applied at save time
--   TransportAmount     : flat customer transport fee auto-applied at save time
--   VatTypeSnapshot     : "Inclusive" or "Exclusive" at the time of creation
--
-- Line column (QuotationItems):
--   LineRebateAmount    : share of the flat header rebate allocated to this line
--                         (distributed proportionally to LineSubTotal). For audit
--                         and per-line display in the Edit view.
--
-- Idempotent: safe to run multiple times. Uses named defaults so the script
-- can be rolled back cleanly if needed.
-- =========================================================================

IF COL_LENGTH('dbo.Quotations', 'RebateAmount') IS NULL
BEGIN
    ALTER TABLE dbo.Quotations
        ADD RebateAmount DECIMAL(18,2) NOT NULL
            CONSTRAINT DF_Quotations_RebateAmount DEFAULT 0;
END;
GO

IF COL_LENGTH('dbo.Quotations', 'TransportAmount') IS NULL
BEGIN
    ALTER TABLE dbo.Quotations
        ADD TransportAmount DECIMAL(18,2) NOT NULL
            CONSTRAINT DF_Quotations_TransportAmount DEFAULT 0;
END;
GO

IF COL_LENGTH('dbo.Quotations', 'VatTypeSnapshot') IS NULL
BEGIN
    ALTER TABLE dbo.Quotations
        ADD VatTypeSnapshot NVARCHAR(20) NULL;
END;
GO

IF COL_LENGTH('dbo.QuotationItems', 'LineRebateAmount') IS NULL
BEGIN
    ALTER TABLE dbo.QuotationItems
        ADD LineRebateAmount DECIMAL(18,2) NOT NULL
            CONSTRAINT DF_QuotationItems_LineRebateAmount DEFAULT 0;
END;
GO

PRINT 'Quotation VAT / Rebate / Transport audit columns added.';
GO
