-- =========================================================================
-- Quarry Management System - WeighmentTransactions: Prepayment selection
--
-- Adds optional FK columns linking a weighment to a specific prepayment and
-- line item the operator picked at the scale. Later, when the weighment is
-- converted to an invoice, ApplyPrepaymentToInvoiceAsync drains this
-- selected prepayment first before falling back to FIFO on other active
-- prepayments for the customer.
--
-- Also updates the default constraint on WeightUnit from 'kg' to 'Ton' so
-- new weighments default to tons. Existing rows are NOT re-unitized \u2014 their
-- WeightUnit stays as whatever was stored (likely 'kg'). CalculateFinancials
-- still branches on WeightUnit so mixed-unit history renders correctly.
--
-- Idempotent: safe to re-run.
-- =========================================================================

-- 1. SelectedPrepaymentId  -> CustomerPrepayments(Id)
IF COL_LENGTH('dbo.WeighmentTransactions', 'SelectedPrepaymentId') IS NULL
BEGIN
    ALTER TABLE dbo.WeighmentTransactions
        ADD SelectedPrepaymentId INT NULL;
END;
GO

-- 2. SelectedPrepaymentLineItemId -> PrepaymentLineItems(Id)
IF COL_LENGTH('dbo.WeighmentTransactions', 'SelectedPrepaymentLineItemId') IS NULL
BEGIN
    ALTER TABLE dbo.WeighmentTransactions
        ADD SelectedPrepaymentLineItemId INT NULL;
END;
GO

-- 3. FK to CustomerPrepayments(Id) with NO ACTION on delete (Restrict).
--    Matches EF HasOne/WithMany/OnDelete(Restrict) mapping.
IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE name = 'FK_WeighmentTransactions_CustomerPrepayments_SelectedPrepaymentId'
)
BEGIN
    ALTER TABLE dbo.WeighmentTransactions
        ADD CONSTRAINT FK_WeighmentTransactions_CustomerPrepayments_SelectedPrepaymentId
        FOREIGN KEY (SelectedPrepaymentId)
        REFERENCES dbo.CustomerPrepayments(Id)
        ON DELETE NO ACTION;
END;
GO

-- 4. FK to PrepaymentLineItems(Id) with NO ACTION on delete (Restrict).
IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE name = 'FK_WeighmentTransactions_PrepaymentLineItems_SelectedPrepaymentLineItemId'
)
BEGIN
    ALTER TABLE dbo.WeighmentTransactions
        ADD CONSTRAINT FK_WeighmentTransactions_PrepaymentLineItems_SelectedPrepaymentLineItemId
        FOREIGN KEY (SelectedPrepaymentLineItemId)
        REFERENCES dbo.PrepaymentLineItems(Id)
        ON DELETE NO ACTION;
END;
GO

-- 5. Helpful non-unique indexes for the FK columns so drain lookups stay fast.
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_WeighmentTransactions_SelectedPrepaymentId'
      AND object_id = OBJECT_ID('dbo.WeighmentTransactions')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_WeighmentTransactions_SelectedPrepaymentId
        ON dbo.WeighmentTransactions(SelectedPrepaymentId)
        WHERE SelectedPrepaymentId IS NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_WeighmentTransactions_SelectedPrepaymentLineItemId'
      AND object_id = OBJECT_ID('dbo.WeighmentTransactions')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_WeighmentTransactions_SelectedPrepaymentLineItemId
        ON dbo.WeighmentTransactions(SelectedPrepaymentLineItemId)
        WHERE SelectedPrepaymentLineItemId IS NOT NULL;
END;
GO

-- 6. Swap the WeightUnit default from 'kg' to 'Ton'. We drop the existing
--    default (whatever its generated name is) and re-add ours with a known
--    name so future migrations can reference it predictably.
DECLARE @defaultName NVARCHAR(128);

SELECT @defaultName = dc.name
FROM sys.default_constraints dc
JOIN sys.columns c
  ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
WHERE dc.parent_object_id = OBJECT_ID('dbo.WeighmentTransactions')
  AND c.name = 'WeightUnit';

IF @defaultName IS NOT NULL
BEGIN
    EXEC('ALTER TABLE dbo.WeighmentTransactions DROP CONSTRAINT ' + @defaultName);
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.default_constraints
    WHERE name = 'DF_WeighmentTransactions_WeightUnit'
)
BEGIN
    ALTER TABLE dbo.WeighmentTransactions
        ADD CONSTRAINT DF_WeighmentTransactions_WeightUnit
        DEFAULT 'Ton' FOR WeightUnit;
END;
GO

PRINT 'WeighmentTransactions: prepayment-selection FKs + Ton default installed.';
GO
