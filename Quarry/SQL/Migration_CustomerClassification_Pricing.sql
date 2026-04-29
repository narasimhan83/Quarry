-- =========================================================================
-- Quarry Management System — Customer classification + per-customer pricing
--
-- Adds:
--   * CustomerTypes lookup (seeded: Dealer, Supplier)
--   * VatTypes      lookup (seeded: Inclusive, Exclusive)
--   * CustomerMaterialPrices history table (one row per price change)
--   * New Customers columns: CustomerTypeId, VatTypeId, HasRebate,
--       RebateAmount, TransportRequired, TransportAmount
--   * New Invoices columns: RebateAmount, TransportAmount, VatTypeSnapshot
--
-- Idempotent: every block is guarded so repeated runs are safe.
--
-- IMPORTANT: The `GO` separators are mandatory. They split the script into
-- batches so that columns/tables created earlier become visible to later
-- batches (CREATE INDEX, ALTER TABLE ADD CONSTRAINT that references a
-- just-added column, etc.). Removing them will make SQL Server throw
-- "Invalid column name" at parse time.
-- =========================================================================

------------------------------------------------------------------------
-- 1. CustomerTypes lookup
------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CustomerTypes')
BEGIN
    CREATE TABLE dbo.CustomerTypes (
        Id        INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        Name      NVARCHAR(50) NOT NULL,
        IsActive  BIT NOT NULL CONSTRAINT DF_CustomerTypes_IsActive DEFAULT 1,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_CustomerTypes_CreatedAt DEFAULT SYSDATETIME()
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.CustomerTypes WHERE Name = 'Dealer')
BEGIN
    SET IDENTITY_INSERT dbo.CustomerTypes ON;
    INSERT INTO dbo.CustomerTypes (Id, Name, IsActive, CreatedAt) VALUES (1, 'Dealer', 1, '2024-01-01');
    SET IDENTITY_INSERT dbo.CustomerTypes OFF;
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.CustomerTypes WHERE Name = 'Supplier')
BEGIN
    SET IDENTITY_INSERT dbo.CustomerTypes ON;
    INSERT INTO dbo.CustomerTypes (Id, Name, IsActive, CreatedAt) VALUES (2, 'Supplier', 1, '2024-01-01');
    SET IDENTITY_INSERT dbo.CustomerTypes OFF;
END;
GO

------------------------------------------------------------------------
-- 2. VatTypes lookup
------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'VatTypes')
BEGIN
    CREATE TABLE dbo.VatTypes (
        Id        INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        Name      NVARCHAR(30) NOT NULL,
        IsActive  BIT NOT NULL CONSTRAINT DF_VatTypes_IsActive DEFAULT 1,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_VatTypes_CreatedAt DEFAULT SYSDATETIME()
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.VatTypes WHERE Name = 'Inclusive')
BEGIN
    SET IDENTITY_INSERT dbo.VatTypes ON;
    INSERT INTO dbo.VatTypes (Id, Name, IsActive, CreatedAt) VALUES (1, 'Inclusive', 1, '2024-01-01');
    SET IDENTITY_INSERT dbo.VatTypes OFF;
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.VatTypes WHERE Name = 'Exclusive')
BEGIN
    SET IDENTITY_INSERT dbo.VatTypes ON;
    INSERT INTO dbo.VatTypes (Id, Name, IsActive, CreatedAt) VALUES (2, 'Exclusive', 1, '2024-01-01');
    SET IDENTITY_INSERT dbo.VatTypes OFF;
END;
GO

------------------------------------------------------------------------
-- 3. New columns on Customers
------------------------------------------------------------------------
IF COL_LENGTH('dbo.Customers', 'CustomerTypeId') IS NULL
BEGIN
    ALTER TABLE dbo.Customers ADD CustomerTypeId INT NULL;
END;
GO

IF COL_LENGTH('dbo.Customers', 'VatTypeId') IS NULL
BEGIN
    ALTER TABLE dbo.Customers ADD VatTypeId INT NULL;
END;
GO

IF COL_LENGTH('dbo.Customers', 'HasRebate') IS NULL
BEGIN
    ALTER TABLE dbo.Customers ADD HasRebate BIT NOT NULL
        CONSTRAINT DF_Customers_HasRebate DEFAULT 0;
END;
GO

IF COL_LENGTH('dbo.Customers', 'RebateAmount') IS NULL
BEGIN
    ALTER TABLE dbo.Customers ADD RebateAmount DECIMAL(18,2) NULL;
END;
GO

IF COL_LENGTH('dbo.Customers', 'TransportRequired') IS NULL
BEGIN
    ALTER TABLE dbo.Customers ADD TransportRequired BIT NOT NULL
        CONSTRAINT DF_Customers_TransportRequired DEFAULT 0;
END;
GO

IF COL_LENGTH('dbo.Customers', 'TransportAmount') IS NULL
BEGIN
    ALTER TABLE dbo.Customers ADD TransportAmount DECIMAL(18,2) NULL;
END;
GO

-- FK: Customers.CustomerTypeId -> CustomerTypes.Id
IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Customers_CustomerTypes'
)
BEGIN
    ALTER TABLE dbo.Customers
        ADD CONSTRAINT FK_Customers_CustomerTypes
            FOREIGN KEY (CustomerTypeId) REFERENCES dbo.CustomerTypes(Id);
END;
GO

-- FK: Customers.VatTypeId -> VatTypes.Id
IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Customers_VatTypes'
)
BEGIN
    ALTER TABLE dbo.Customers
        ADD CONSTRAINT FK_Customers_VatTypes
            FOREIGN KEY (VatTypeId) REFERENCES dbo.VatTypes(Id);
END;
GO

-- Optional helper indexes for the new FK columns
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_Customers_CustomerTypeId'
      AND object_id = OBJECT_ID('dbo.Customers')
)
BEGIN
    CREATE INDEX IX_Customers_CustomerTypeId ON dbo.Customers(CustomerTypeId)
        WHERE CustomerTypeId IS NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_Customers_VatTypeId'
      AND object_id = OBJECT_ID('dbo.Customers')
)
BEGIN
    CREATE INDEX IX_Customers_VatTypeId ON dbo.Customers(VatTypeId)
        WHERE VatTypeId IS NOT NULL;
END;
GO

------------------------------------------------------------------------
-- 4. CustomerMaterialPrices (full history; IsCurrent denormalized flag)
------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CustomerMaterialPrices')
BEGIN
    CREATE TABLE dbo.CustomerMaterialPrices (
        Id             INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        CustomerId     INT NOT NULL,
        MaterialId     INT NOT NULL,
        UnitPrice      DECIMAL(18,2) NOT NULL,
        VatRate        DECIMAL(5,2) NULL,
        EffectiveFrom  DATETIME2 NOT NULL CONSTRAINT DF_CustomerMaterialPrices_EffFrom DEFAULT SYSDATETIME(),
        IsCurrent      BIT NOT NULL CONSTRAINT DF_CustomerMaterialPrices_IsCurrent DEFAULT 1,
        Notes          NVARCHAR(200) NULL,
        CreatedBy      NVARCHAR(100) NULL,
        CreatedAt      DATETIME2 NOT NULL CONSTRAINT DF_CustomerMaterialPrices_CreatedAt DEFAULT SYSDATETIME(),
        CONSTRAINT FK_CustomerMaterialPrices_Customers
            FOREIGN KEY (CustomerId) REFERENCES dbo.Customers(Id) ON DELETE CASCADE,
        CONSTRAINT FK_CustomerMaterialPrices_Materials
            FOREIGN KEY (MaterialId) REFERENCES dbo.Materials(Id)
    );
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_CustomerMaterialPrices_Customer_Material_Eff'
      AND object_id = OBJECT_ID('dbo.CustomerMaterialPrices')
)
BEGIN
    CREATE INDEX IX_CustomerMaterialPrices_Customer_Material_Eff
        ON dbo.CustomerMaterialPrices(CustomerId, MaterialId, EffectiveFrom DESC);
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_CustomerMaterialPrices_Current'
      AND object_id = OBJECT_ID('dbo.CustomerMaterialPrices')
)
BEGIN
    CREATE INDEX IX_CustomerMaterialPrices_Current
        ON dbo.CustomerMaterialPrices(CustomerId, MaterialId)
        WHERE IsCurrent = 1;
END;
GO

------------------------------------------------------------------------
-- 5. New columns on Invoices
------------------------------------------------------------------------
IF COL_LENGTH('dbo.Invoices', 'RebateAmount') IS NULL
BEGIN
    ALTER TABLE dbo.Invoices ADD RebateAmount DECIMAL(18,2) NOT NULL
        CONSTRAINT DF_Invoices_RebateAmount DEFAULT 0;
END;
GO

IF COL_LENGTH('dbo.Invoices', 'TransportAmount') IS NULL
BEGIN
    ALTER TABLE dbo.Invoices ADD TransportAmount DECIMAL(18,2) NOT NULL
        CONSTRAINT DF_Invoices_TransportAmount DEFAULT 0;
END;
GO

IF COL_LENGTH('dbo.Invoices', 'VatTypeSnapshot') IS NULL
BEGIN
    ALTER TABLE dbo.Invoices ADD VatTypeSnapshot NVARCHAR(20) NULL;
END;
GO

PRINT 'Customer classification + per-customer pricing migration complete.';
GO
