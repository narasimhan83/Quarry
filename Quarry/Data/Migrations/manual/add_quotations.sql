-- Add Quotations and QuotationItems tables if not exists
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Quotations]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Quotations](
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [QuotationNumber] NVARCHAR(50) NOT NULL,
        [CustomerId] INT NOT NULL,
        [QuotationDate] DATETIME2 NOT NULL,
        [ExpiryDate] DATETIME2 NULL,
        [SubTotal] DECIMAL(18,2) NOT NULL CONSTRAINT DF_Quotations_SubTotal DEFAULT(0),
        [VatAmount] DECIMAL(18,2) NOT NULL CONSTRAINT DF_Quotations_VatAmount DEFAULT(0),
        [TotalAmount] DECIMAL(18,2) NOT NULL CONSTRAINT DF_Quotations_TotalAmount DEFAULT(0),
        [Status] NVARCHAR(20) NOT NULL CONSTRAINT DF_Quotations_Status DEFAULT('Draft'),
        [Notes] NVARCHAR(500) NULL,
        [CreatedBy] NVARCHAR(100) NULL,
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT DF_Quotations_CreatedAt DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] DATETIME2 NULL
    );
    ALTER TABLE [dbo].[Quotations] ADD CONSTRAINT UQ_Quotations_QuotationNumber UNIQUE ([QuotationNumber]);
    ALTER TABLE [dbo].[Quotations] ADD CONSTRAINT FK_Quotations_Customers_CustomerId FOREIGN KEY ([CustomerId]) REFERENCES [dbo].[Customers]([Id]) ON DELETE NO ACTION ON UPDATE NO ACTION;
END
GO

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[QuotationItems]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[QuotationItems](
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [QuotationId] INT NOT NULL,
        [MaterialId] INT NULL,
        [Description] NVARCHAR(200) NULL,
        [Quantity] DECIMAL(18,2) NOT NULL CONSTRAINT DF_QuotationItems_Quantity DEFAULT(0),
        [Unit] NVARCHAR(20) NOT NULL CONSTRAINT DF_QuotationItems_Unit DEFAULT('Ton'),
        [UnitPrice] DECIMAL(18,2) NOT NULL CONSTRAINT DF_QuotationItems_UnitPrice DEFAULT(0),
        [VatRate] DECIMAL(5,2) NOT NULL CONSTRAINT DF_QuotationItems_VatRate DEFAULT(7.5),
        [LineSubTotal] DECIMAL(18,2) NOT NULL CONSTRAINT DF_QuotationItems_LineSubTotal DEFAULT(0),
        [LineVatAmount] DECIMAL(18,2) NOT NULL CONSTRAINT DF_QuotationItems_LineVatAmount DEFAULT(0),
        [LineTotal] DECIMAL(18,2) NOT NULL CONSTRAINT DF_QuotationItems_LineTotal DEFAULT(0)
    );
    CREATE INDEX IX_QuotationItems_QuotationId ON [dbo].[QuotationItems]([QuotationId]);
    ALTER TABLE [dbo].[QuotationItems] ADD CONSTRAINT FK_QuotationItems_Quotations_QuotationId FOREIGN KEY ([QuotationId]) REFERENCES [dbo].[Quotations]([Id]) ON DELETE CASCADE ON UPDATE NO ACTION;
    IF OBJECT_ID(N'[dbo].[Materials]', N'U') IS NOT NULL
    BEGIN
        ALTER TABLE [dbo].[QuotationItems] ADD CONSTRAINT FK_QuotationItems_Materials_MaterialId FOREIGN KEY ([MaterialId]) REFERENCES [dbo].[Materials]([Id]) ON DELETE NO ACTION ON UPDATE NO ACTION;
    END
END
GO