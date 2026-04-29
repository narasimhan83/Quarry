-- =========================================================================
-- Quarry Management System — Prepayment multi-material line items migration
--
-- Run this once against your QuarryManagementNG database. It is idempotent:
-- safe to run multiple times thanks to IF NOT EXISTS / IF COL_LENGTH guards.
--
-- NOTE: The GO separators matter. They split this into batches so that
-- columns added by ALTER TABLE are visible to subsequent CREATE INDEX
-- statements. SSMS parses each batch independently.
-- =========================================================================

-- 1. Make MaterialId and WeightUnit on CustomerPrepayments optional -------
IF EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.CustomerPrepayments')
      AND name = 'MaterialId'
      AND is_nullable = 0
)
BEGIN
    ALTER TABLE dbo.CustomerPrepayments ALTER COLUMN MaterialId INT NULL;
END;
GO

IF EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.CustomerPrepayments')
      AND name = 'WeightUnit'
      AND is_nullable = 0
)
BEGIN
    ALTER TABLE dbo.CustomerPrepayments ALTER COLUMN WeightUnit NVARCHAR(10) NULL;
END;
GO

-- 2. Add ExpectedPickupDate column ---------------------------------------
IF COL_LENGTH('dbo.CustomerPrepayments', 'ExpectedPickupDate') IS NULL
BEGIN
    ALTER TABLE dbo.CustomerPrepayments ADD ExpectedPickupDate DATETIME2 NULL;
END;
GO

-- 3. Create PrepaymentLineItems table -------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM sys.tables WHERE name = 'PrepaymentLineItems'
)
BEGIN
    CREATE TABLE dbo.PrepaymentLineItems (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        CustomerPrepaymentId INT NOT NULL,
        MaterialId INT NOT NULL,
        Quantity DECIMAL(18,3) NOT NULL,
        Unit NVARCHAR(20) NOT NULL,
        UnitPrice DECIMAL(18,2) NOT NULL,
        LineTotal DECIMAL(18,2) NOT NULL,
        UsedQuantity DECIMAL(18,3) NOT NULL CONSTRAINT DF_PrepaymentLineItems_UsedQty DEFAULT 0,
        UsedAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_PrepaymentLineItems_UsedAmt DEFAULT 0,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_PrepaymentLineItems_CreatedAt DEFAULT SYSDATETIME(),
        CONSTRAINT FK_PrepaymentLineItems_CustomerPrepayments
            FOREIGN KEY (CustomerPrepaymentId) REFERENCES dbo.CustomerPrepayments(Id) ON DELETE CASCADE,
        CONSTRAINT FK_PrepaymentLineItems_Materials
            FOREIGN KEY (MaterialId) REFERENCES dbo.Materials(Id)
    );
END;
GO

-- Indexes on PrepaymentLineItems. Separate batch so the CREATE TABLE above
-- is committed before these run.
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_PrepaymentLineItems_CustomerPrepaymentId'
      AND object_id = OBJECT_ID('dbo.PrepaymentLineItems')
)
BEGIN
    CREATE INDEX IX_PrepaymentLineItems_CustomerPrepaymentId
        ON dbo.PrepaymentLineItems(CustomerPrepaymentId);
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_PrepaymentLineItems_MaterialId'
      AND object_id = OBJECT_ID('dbo.PrepaymentLineItems')
)
BEGIN
    CREATE INDEX IX_PrepaymentLineItems_MaterialId
        ON dbo.PrepaymentLineItems(MaterialId);
END;
GO

-- 4. Add PrepaymentLineItemId column to PrepaymentApplications ------------
IF COL_LENGTH('dbo.PrepaymentApplications', 'PrepaymentLineItemId') IS NULL
BEGIN
    ALTER TABLE dbo.PrepaymentApplications ADD PrepaymentLineItemId INT NULL;
END;
GO

-- FK constraint for the new column (separate batch so the column exists)
IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE name = 'FK_PrepaymentApplications_PrepaymentLineItems'
)
BEGIN
    ALTER TABLE dbo.PrepaymentApplications
        ADD CONSTRAINT FK_PrepaymentApplications_PrepaymentLineItems
            FOREIGN KEY (PrepaymentLineItemId)
            REFERENCES dbo.PrepaymentLineItems(Id);
END;
GO

-- Filtered index for the new column (separate batch for the same reason)
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_PrepaymentApplications_PrepaymentLineItemId'
      AND object_id = OBJECT_ID('dbo.PrepaymentApplications')
)
BEGIN
    CREATE INDEX IX_PrepaymentApplications_PrepaymentLineItemId
        ON dbo.PrepaymentApplications(PrepaymentLineItemId)
        WHERE PrepaymentLineItemId IS NOT NULL;
END;
GO

-- 5. Seed the Opening Balance Equity account if it doesn't exist ----------
IF NOT EXISTS (SELECT 1 FROM dbo.ChartOfAccounts WHERE AccountCode = '3102')
BEGIN
    INSERT INTO dbo.ChartOfAccounts
        (AccountCode, AccountName, AccountType, SubType, OpeningBalance, CurrentBalance, IsActive, CreatedAt)
    VALUES
        ('3102', 'Opening Balance Equity', 'Equity', 'Capital', 0, 0, 1, SYSDATETIME());
END;
GO

PRINT 'Prepayment multi-material migration complete.';
GO
