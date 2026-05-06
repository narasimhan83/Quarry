-- ==========================================================================
-- Inventory Phase 1: schema only
-- ==========================================================================
-- Adds the foundational tables for the inventory + production system. No
-- behaviour yet \u2014 these tables are populated by Phase 2 controllers and
-- consumed by Phase 3 weighbridge integration. See inventory_roadmap.md.
--
-- Naming convention:
--   - Plural table names (RawMaterials, ProductionRuns, ...).
--   - PK is "Id" (not "RawMaterialId" etc.) for consistency with the rest
--     of the schema (Customers.Id, Materials.Id).
--   - FK names follow EF's convention: FK_{table}_{principal}_{column}.
--
-- Designed to be idempotent: each CREATE is wrapped in IF NOT EXISTS.
-- Re-runnable in case it's executed against a partially-applied DB.
-- ==========================================================================

SET NOCOUNT ON;
GO

-- --------------------------------------------------------------------------
-- 1. RawMaterials
-- --------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'RawMaterials')
BEGIN
    CREATE TABLE [dbo].[RawMaterials](
        [Id]        INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [Name]      NVARCHAR(100) NOT NULL,
        [Unit]      NVARCHAR(20)  NOT NULL CONSTRAINT DF_RawMaterials_Unit DEFAULT ('Ton'),
        [Status]    NVARCHAR(20)  NOT NULL CONSTRAINT DF_RawMaterials_Status DEFAULT ('Active'),
        [CreatedAt] DATETIME2(7)  NOT NULL CONSTRAINT DF_RawMaterials_CreatedAt DEFAULT (SYSUTCDATETIME())
    );
END
GO

-- --------------------------------------------------------------------------
-- 2. RawMaterialReceipts
-- --------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'RawMaterialReceipts')
BEGIN
    CREATE TABLE [dbo].[RawMaterialReceipts](
        [Id]            INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [ReceiptNumber] NVARCHAR(50)  NOT NULL,
        [QuarryId]      INT           NOT NULL,
        [RawMaterialId] INT           NOT NULL,
        [ReceiptDate]   DATETIME2(7)  NOT NULL CONSTRAINT DF_RMR_ReceiptDate DEFAULT (SYSUTCDATETIME()),
        [Quantity]      DECIMAL(18,3) NOT NULL,
        [UnitCost]      DECIMAL(18,2) NOT NULL CONSTRAINT DF_RMR_UnitCost   DEFAULT (0),
        [Source]        NVARCHAR(200) NULL,
        [Notes]         NVARCHAR(500) NULL,
        [CreatedBy]     NVARCHAR(100) NULL,
        [CreatedAt]     DATETIME2(7)  NOT NULL CONSTRAINT DF_RMR_CreatedAt  DEFAULT (SYSUTCDATETIME())
    );

    ALTER TABLE [dbo].[RawMaterialReceipts]
        ADD CONSTRAINT FK_RawMaterialReceipts_Quarries_QuarryId
        FOREIGN KEY ([QuarryId])
        REFERENCES [dbo].[Quarries]([Id])
        ON DELETE NO ACTION;

    ALTER TABLE [dbo].[RawMaterialReceipts]
        ADD CONSTRAINT FK_RawMaterialReceipts_RawMaterials_RawMaterialId
        FOREIGN KEY ([RawMaterialId])
        REFERENCES [dbo].[RawMaterials]([Id])
        ON DELETE NO ACTION;

    CREATE UNIQUE INDEX UX_RawMaterialReceipts_ReceiptNumber
        ON [dbo].[RawMaterialReceipts]([ReceiptNumber]);
    CREATE INDEX IX_RawMaterialReceipts_Quarry_RawMaterial_Date
        ON [dbo].[RawMaterialReceipts]([QuarryId], [RawMaterialId], [ReceiptDate]);
END
GO

-- --------------------------------------------------------------------------
-- 3. ProductionRuns
-- --------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ProductionRuns')
BEGIN
    CREATE TABLE [dbo].[ProductionRuns](
        [Id]              INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [RunNumber]       NVARCHAR(50)  NOT NULL,
        [QuarryId]        INT           NOT NULL,
        [RunDate]         DATETIME2(7)  NOT NULL CONSTRAINT DF_PR_RunDate DEFAULT (SYSUTCDATETIME()),
        [RawMaterialId]   INT           NOT NULL,
        [InputQuantity]   DECIMAL(18,3) NOT NULL,
        [InputTotalCost] DECIMAL(18,2) NOT NULL CONSTRAINT DF_PR_InputCost DEFAULT (0),
        [WasteQuantity]   DECIMAL(18,3) NOT NULL CONSTRAINT DF_PR_Waste DEFAULT (0),
        [Status]          NVARCHAR(20)  NOT NULL CONSTRAINT DF_PR_Status DEFAULT ('Draft'),
        [Operator]        NVARCHAR(200) NULL,
        [Notes]           NVARCHAR(500) NULL,
        [CreatedBy]       NVARCHAR(100) NULL,
        [CreatedAt]       DATETIME2(7)  NOT NULL CONSTRAINT DF_PR_CreatedAt DEFAULT (SYSUTCDATETIME()),
        [PostedAt]        DATETIME2(7)  NULL
    );

    ALTER TABLE [dbo].[ProductionRuns]
        ADD CONSTRAINT FK_ProductionRuns_Quarries_QuarryId
        FOREIGN KEY ([QuarryId])
        REFERENCES [dbo].[Quarries]([Id])
        ON DELETE NO ACTION;

    ALTER TABLE [dbo].[ProductionRuns]
        ADD CONSTRAINT FK_ProductionRuns_RawMaterials_RawMaterialId
        FOREIGN KEY ([RawMaterialId])
        REFERENCES [dbo].[RawMaterials]([Id])
        ON DELETE NO ACTION;

    CREATE UNIQUE INDEX UX_ProductionRuns_RunNumber
        ON [dbo].[ProductionRuns]([RunNumber]);
    CREATE INDEX IX_ProductionRuns_Quarry_Date
        ON [dbo].[ProductionRuns]([QuarryId], [RunDate]);
END
GO

-- --------------------------------------------------------------------------
-- 4. ProductionRunOutputs
-- --------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ProductionRunOutputs')
BEGIN
    CREATE TABLE [dbo].[ProductionRunOutputs](
        [Id]              INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [ProductionRunId] INT           NOT NULL,
        [MaterialId]      INT           NOT NULL,
        [Quantity]        DECIMAL(18,3) NOT NULL,
        [AllocatedCost]   DECIMAL(18,2) NOT NULL CONSTRAINT DF_PRO_AllocatedCost DEFAULT (0)
    );

    ALTER TABLE [dbo].[ProductionRunOutputs]
        ADD CONSTRAINT FK_ProductionRunOutputs_ProductionRuns_ProductionRunId
        FOREIGN KEY ([ProductionRunId])
        REFERENCES [dbo].[ProductionRuns]([Id])
        ON DELETE CASCADE;

    ALTER TABLE [dbo].[ProductionRunOutputs]
        ADD CONSTRAINT FK_ProductionRunOutputs_Materials_MaterialId
        FOREIGN KEY ([MaterialId])
        REFERENCES [dbo].[Materials]([Id])
        ON DELETE NO ACTION;

    CREATE INDEX IX_ProductionRunOutputs_Run_Material
        ON [dbo].[ProductionRunOutputs]([ProductionRunId], [MaterialId]);
END
GO

-- --------------------------------------------------------------------------
-- 5. StockMovements (audit log)
-- --------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'StockMovements')
BEGIN
    CREATE TABLE [dbo].[StockMovements](
        [Id]                      INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [MovementDate]            DATETIME2(7)  NOT NULL CONSTRAINT DF_SM_MovementDate DEFAULT (SYSUTCDATETIME()),
        [QuarryId]                INT           NOT NULL,
        [MaterialId]              INT           NULL,
        [RawMaterialId]           INT           NULL,
        [MovementType]            NVARCHAR(30)  NOT NULL,
        [Quantity]                DECIMAL(18,3) NOT NULL,
        [UnitCost]                DECIMAL(18,4) NOT NULL CONSTRAINT DF_SM_UnitCost DEFAULT (0),
        [RawMaterialReceiptId]    INT           NULL,
        [ProductionRunId]         INT           NULL,
        [ProductionRunOutputId]   INT           NULL,
        [WeighmentTransactionId]  INT           NULL,
        [Notes]                   NVARCHAR(500) NULL,
        [CreatedBy]               NVARCHAR(100) NULL,
        [CreatedAt]               DATETIME2(7)  NOT NULL CONSTRAINT DF_SM_CreatedAt DEFAULT (SYSUTCDATETIME())
    );

    -- All FKs use NO ACTION / SET NULL so we never lose audit history when
    -- a master record is removed. Phase 2 controllers should refuse to delete
    -- a master row that has movements anyway.
    ALTER TABLE [dbo].[StockMovements] ADD CONSTRAINT FK_StockMovements_Quarries_QuarryId
        FOREIGN KEY ([QuarryId]) REFERENCES [dbo].[Quarries]([Id]) ON DELETE NO ACTION;
    ALTER TABLE [dbo].[StockMovements] ADD CONSTRAINT FK_StockMovements_Materials_MaterialId
        FOREIGN KEY ([MaterialId]) REFERENCES [dbo].[Materials]([Id]) ON DELETE NO ACTION;
    ALTER TABLE [dbo].[StockMovements] ADD CONSTRAINT FK_StockMovements_RawMaterials_RawMaterialId
        FOREIGN KEY ([RawMaterialId]) REFERENCES [dbo].[RawMaterials]([Id]) ON DELETE NO ACTION;
    ALTER TABLE [dbo].[StockMovements] ADD CONSTRAINT FK_StockMovements_RawMaterialReceipts_RawMaterialReceiptId
        FOREIGN KEY ([RawMaterialReceiptId]) REFERENCES [dbo].[RawMaterialReceipts]([Id]) ON DELETE SET NULL;
    ALTER TABLE [dbo].[StockMovements] ADD CONSTRAINT FK_StockMovements_ProductionRuns_ProductionRunId
        FOREIGN KEY ([ProductionRunId]) REFERENCES [dbo].[ProductionRuns]([Id]) ON DELETE SET NULL;
    ALTER TABLE [dbo].[StockMovements] ADD CONSTRAINT FK_StockMovements_ProductionRunOutputs_ProductionRunOutputId
        FOREIGN KEY ([ProductionRunOutputId]) REFERENCES [dbo].[ProductionRunOutputs]([Id]) ON DELETE SET NULL;
    ALTER TABLE [dbo].[StockMovements] ADD CONSTRAINT FK_StockMovements_WeighmentTransactions_WeighmentTransactionId
        FOREIGN KEY ([WeighmentTransactionId]) REFERENCES [dbo].[WeighmentTransactions]([Id]) ON DELETE SET NULL;

    CREATE INDEX IX_StockMovements_Quarry_Material_Date
        ON [dbo].[StockMovements]([QuarryId], [MaterialId], [MovementDate]);
    CREATE INDEX IX_StockMovements_Quarry_RawMaterial_Date
        ON [dbo].[StockMovements]([QuarryId], [RawMaterialId], [MovementDate]);
    CREATE INDEX IX_StockMovements_MovementType
        ON [dbo].[StockMovements]([MovementType]);
END
GO

-- --------------------------------------------------------------------------
-- 6. MaterialCostStates (running WAC cache)
-- --------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'MaterialCostStates')
BEGIN
    CREATE TABLE [dbo].[MaterialCostStates](
        [Id]              INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [QuarryId]        INT           NOT NULL,
        [MaterialId]      INT           NULL,
        [RawMaterialId]   INT           NULL,
        [QuantityOnHand]  DECIMAL(18,3) NOT NULL CONSTRAINT DF_MCS_Qty DEFAULT (0),
        [TotalCostOnHand] DECIMAL(18,2) NOT NULL CONSTRAINT DF_MCS_Total DEFAULT (0),
        [LastUpdated]     DATETIME2(7)  NOT NULL CONSTRAINT DF_MCS_LastUpdated DEFAULT (SYSUTCDATETIME())
    );

    ALTER TABLE [dbo].[MaterialCostStates] ADD CONSTRAINT FK_MaterialCostStates_Quarries_QuarryId
        FOREIGN KEY ([QuarryId]) REFERENCES [dbo].[Quarries]([Id]) ON DELETE NO ACTION;
    ALTER TABLE [dbo].[MaterialCostStates] ADD CONSTRAINT FK_MaterialCostStates_Materials_MaterialId
        FOREIGN KEY ([MaterialId]) REFERENCES [dbo].[Materials]([Id]) ON DELETE NO ACTION;
    ALTER TABLE [dbo].[MaterialCostStates] ADD CONSTRAINT FK_MaterialCostStates_RawMaterials_RawMaterialId
        FOREIGN KEY ([RawMaterialId]) REFERENCES [dbo].[RawMaterials]([Id]) ON DELETE NO ACTION;

    -- Filtered unique indexes: enforce one row per (Quarry, Material) for
    -- finished goods and one per (Quarry, RawMaterial) for raw inputs, but
    -- allow both kinds of rows to coexist for the same QuarryId.
    CREATE UNIQUE INDEX UX_MaterialCostStates_Quarry_Material
        ON [dbo].[MaterialCostStates]([QuarryId], [MaterialId])
        WHERE [MaterialId] IS NOT NULL;
    CREATE UNIQUE INDEX UX_MaterialCostStates_Quarry_RawMaterial
        ON [dbo].[MaterialCostStates]([QuarryId], [RawMaterialId])
        WHERE [RawMaterialId] IS NOT NULL;
END
GO

-- --------------------------------------------------------------------------
-- 7. Chart of Accounts seed rows for inventory accounting (Phase 4 will use)
-- --------------------------------------------------------------------------
-- Idempotent inserts \u2014 only add rows that don't exist by AccountCode.
IF NOT EXISTS (SELECT 1 FROM dbo.ChartOfAccounts WHERE AccountCode = '1301')
    INSERT INTO dbo.ChartOfAccounts (AccountCode, AccountName, AccountType, SubType, OpeningBalance, CurrentBalance, IsActive, CreatedAt)
    VALUES ('1301', 'Raw Material Inventory',     'Asset', 'Current', 0, 0, 1, SYSUTCDATETIME());
IF NOT EXISTS (SELECT 1 FROM dbo.ChartOfAccounts WHERE AccountCode = '1302')
    INSERT INTO dbo.ChartOfAccounts (AccountCode, AccountName, AccountType, SubType, OpeningBalance, CurrentBalance, IsActive, CreatedAt)
    VALUES ('1302', 'Finished Goods Inventory',   'Asset', 'Current', 0, 0, 1, SYSUTCDATETIME());
IF NOT EXISTS (SELECT 1 FROM dbo.ChartOfAccounts WHERE AccountCode = '1303')
    INSERT INTO dbo.ChartOfAccounts (AccountCode, AccountName, AccountType, SubType, OpeningBalance, CurrentBalance, IsActive, CreatedAt)
    VALUES ('1303', 'Production WIP',             'Asset', 'Current', 0, 0, 1, SYSUTCDATETIME());
IF NOT EXISTS (SELECT 1 FROM dbo.ChartOfAccounts WHERE AccountCode = '5001')
    INSERT INTO dbo.ChartOfAccounts (AccountCode, AccountName, AccountType, SubType, OpeningBalance, CurrentBalance, IsActive, CreatedAt)
    VALUES ('5001', 'Cost of Goods Sold',         'Expense', NULL,    0, 0, 1, SYSUTCDATETIME());
IF NOT EXISTS (SELECT 1 FROM dbo.ChartOfAccounts WHERE AccountCode = '5002')
    INSERT INTO dbo.ChartOfAccounts (AccountCode, AccountName, AccountType, SubType, OpeningBalance, CurrentBalance, IsActive, CreatedAt)
    VALUES ('5002', 'Production Variance',        'Expense', NULL,    0, 0, 1, SYSUTCDATETIME());
GO
