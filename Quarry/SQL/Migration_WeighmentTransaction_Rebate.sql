-- =========================================================================
-- Quarry Management System — Weighment rebate amount column
--
-- Adds:
--   * WeighmentTransactions.RebateAmount  (nullable decimal(18,2))
--
-- Idempotent: safe to run repeatedly. Legacy rows stay NULL (treated as
-- "no rebate" by the controller's ApplyVatTreatmentAsync), so existing
-- TotalAmount values continue to satisfy Subtotal + VAT for old records.
-- =========================================================================

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.WeighmentTransactions')
      AND name = 'RebateAmount')
BEGIN
    ALTER TABLE dbo.WeighmentTransactions
        ADD RebateAmount DECIMAL(18, 2) NULL;
END;
GO

PRINT 'WeighmentTransactions.RebateAmount migration complete.';
GO
