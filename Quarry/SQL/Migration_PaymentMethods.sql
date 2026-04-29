-- =========================================================================
-- Quarry Management System — PaymentMethods lookup + CustomerPrepayments FK
--
-- Adds:
--   * PaymentMethods lookup table
--       (seeded: Cash, Bank Transfer, Online Payment, Cheque)
--   * CustomerPrepayments.PaymentMethodId  (nullable FK)
--   * Best-effort backfill of PaymentMethodId from the existing
--     PaymentMethod string column (case-insensitive, whitespace-trimmed)
--
-- Idempotent: every block is guarded so repeated runs are safe.
--
-- IMPORTANT: The `GO` separators are mandatory. They split the script into
-- batches so that tables / columns created earlier become visible to later
-- batches (CREATE INDEX, ALTER TABLE ADD CONSTRAINT that references a
-- just-added column, etc.). Removing them will make SQL Server throw
-- "Invalid column name" at parse time.
-- =========================================================================

------------------------------------------------------------------------
-- 1. PaymentMethods lookup table
------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PaymentMethods')
BEGIN
    CREATE TABLE dbo.PaymentMethods (
        Id            INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        Name          NVARCHAR(50) NOT NULL,
        DisplayOrder  INT          NOT NULL CONSTRAINT DF_PaymentMethods_DisplayOrder DEFAULT 0,
        IsActive      BIT          NOT NULL CONSTRAINT DF_PaymentMethods_IsActive     DEFAULT 1,
        CreatedAt     DATETIME2    NOT NULL CONSTRAINT DF_PaymentMethods_CreatedAt    DEFAULT SYSDATETIME()
    );
END;
GO

------------------------------------------------------------------------
-- 2. Seed the four standard methods. Ids here MUST match the seed data
-- in ApplicationDbContext.OnModelCreating so EF migrations and this
-- script produce the same rows.
------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM dbo.PaymentMethods WHERE Name = 'Cash')
BEGIN
    SET IDENTITY_INSERT dbo.PaymentMethods ON;
    INSERT INTO dbo.PaymentMethods (Id, Name, DisplayOrder, IsActive, CreatedAt)
    VALUES (1, 'Cash', 1, 1, '2024-01-01');
    SET IDENTITY_INSERT dbo.PaymentMethods OFF;
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.PaymentMethods WHERE Name = 'Bank Transfer')
BEGIN
    SET IDENTITY_INSERT dbo.PaymentMethods ON;
    INSERT INTO dbo.PaymentMethods (Id, Name, DisplayOrder, IsActive, CreatedAt)
    VALUES (2, 'Bank Transfer', 2, 1, '2024-01-01');
    SET IDENTITY_INSERT dbo.PaymentMethods OFF;
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.PaymentMethods WHERE Name = 'Online Payment')
BEGIN
    SET IDENTITY_INSERT dbo.PaymentMethods ON;
    INSERT INTO dbo.PaymentMethods (Id, Name, DisplayOrder, IsActive, CreatedAt)
    VALUES (3, 'Online Payment', 3, 1, '2024-01-01');
    SET IDENTITY_INSERT dbo.PaymentMethods OFF;
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.PaymentMethods WHERE Name = 'Cheque')
BEGIN
    SET IDENTITY_INSERT dbo.PaymentMethods ON;
    INSERT INTO dbo.PaymentMethods (Id, Name, DisplayOrder, IsActive, CreatedAt)
    VALUES (4, 'Cheque', 4, 1, '2024-01-01');
    SET IDENTITY_INSERT dbo.PaymentMethods OFF;
END;
GO

------------------------------------------------------------------------
-- 3. Add PaymentMethodId (nullable) + FK to CustomerPrepayments
--
-- Nullable on purpose: legacy rows carry only the PaymentMethod string.
-- We backfill what we can in step 4, but rows with non-standard or
-- blank values stay NULL rather than fail the migration.
------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.CustomerPrepayments')
      AND name = 'PaymentMethodId')
BEGIN
    ALTER TABLE dbo.CustomerPrepayments
        ADD PaymentMethodId INT NULL;
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE name = 'FK_CustomerPrepayments_PaymentMethods_PaymentMethodId')
BEGIN
    ALTER TABLE dbo.CustomerPrepayments
        ADD CONSTRAINT FK_CustomerPrepayments_PaymentMethods_PaymentMethodId
            FOREIGN KEY (PaymentMethodId)
            REFERENCES dbo.PaymentMethods (Id)
            ON DELETE NO ACTION;
END;
GO

-- Supporting index for the FK — speeds up "all prepayments using method X"
-- joins and satisfies SQL Server's recommendation for every FK column.
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_CustomerPrepayments_PaymentMethodId'
      AND object_id = OBJECT_ID(N'dbo.CustomerPrepayments'))
BEGIN
    CREATE INDEX IX_CustomerPrepayments_PaymentMethodId
        ON dbo.CustomerPrepayments (PaymentMethodId);
END;
GO

------------------------------------------------------------------------
-- 4. Best-effort backfill. Match existing legacy strings to the new
-- lookup by name (case-insensitive, trimmed). Rows that don't match any
-- standard method (e.g. "transfer", "POS", "mobile money") stay NULL
-- and will be picked up by the operator on the next Edit — the Edit
-- view falls back to the legacy string when PaymentMethodId is NULL.
--
-- LOWER + LTRIM/RTRIM keeps the match lenient; aliases below cover the
-- two most common variants we've seen in the wild ("transfer" for Bank
-- Transfer, "online" / "pos" for Online Payment).
------------------------------------------------------------------------
UPDATE cp
SET    cp.PaymentMethodId = pm.Id
FROM   dbo.CustomerPrepayments cp
JOIN   dbo.PaymentMethods      pm
       ON LOWER(LTRIM(RTRIM(cp.PaymentMethod))) = LOWER(pm.Name)
WHERE  cp.PaymentMethodId IS NULL
  AND  cp.PaymentMethod IS NOT NULL
  AND  LTRIM(RTRIM(cp.PaymentMethod)) <> '';
GO

-- Aliases for common near-matches. Guarded by IS NULL so we never
-- overwrite a row that the exact-match pass already resolved.
UPDATE dbo.CustomerPrepayments
SET    PaymentMethodId = (SELECT Id FROM dbo.PaymentMethods WHERE Name = 'Bank Transfer')
WHERE  PaymentMethodId IS NULL
  AND  LOWER(LTRIM(RTRIM(ISNULL(PaymentMethod, '')))) IN ('transfer', 'bank', 'banktransfer', 'wire');
GO

UPDATE dbo.CustomerPrepayments
SET    PaymentMethodId = (SELECT Id FROM dbo.PaymentMethods WHERE Name = 'Online Payment')
WHERE  PaymentMethodId IS NULL
  AND  LOWER(LTRIM(RTRIM(ISNULL(PaymentMethod, '')))) IN ('online', 'pos', 'card', 'paystack', 'flutterwave', 'ussd', 'mobile', 'mobile money');
GO

UPDATE dbo.CustomerPrepayments
SET    PaymentMethodId = (SELECT Id FROM dbo.PaymentMethods WHERE Name = 'Cheque')
WHERE  PaymentMethodId IS NULL
  AND  LOWER(LTRIM(RTRIM(ISNULL(PaymentMethod, '')))) IN ('check', 'cheq');
GO

PRINT 'PaymentMethods migration complete.';
GO
