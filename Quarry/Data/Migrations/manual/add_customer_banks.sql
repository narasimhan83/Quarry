-- Add CustomerBanks table for per-customer bank-account registration.
-- Two columns are required (AccountNumber and BankName); the rest are optional.
-- A unique index on (CustomerId, AccountNumber) prevents the same account being
-- registered twice against the same customer; two different customers may still
-- legally share an account number (rare).

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[CustomerBanks]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[CustomerBanks](
        [CustomerBankId]  INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [CustomerId]      INT NOT NULL,
        [AccountNumber]   NVARCHAR(50)  NOT NULL,
        [BankName]        NVARCHAR(150) NOT NULL,
        [BankAddress]     NVARCHAR(255) NULL,
        [BankBranch]      NVARCHAR(150) NULL,
        [BankSwiftCode]   NVARCHAR(20)  NULL,
        [IsActive]        BIT NOT NULL CONSTRAINT DF_CustomerBanks_IsActive  DEFAULT (1),
        [CreatedAt]       DATETIME2(7) NOT NULL CONSTRAINT DF_CustomerBanks_CreatedAt DEFAULT (SYSUTCDATETIME())
    );

    ALTER TABLE [dbo].[CustomerBanks]
        ADD CONSTRAINT FK_CustomerBanks_Customers_CustomerId
        FOREIGN KEY ([CustomerId])
        REFERENCES [dbo].[Customers]([Id])
        ON DELETE CASCADE;

    CREATE UNIQUE INDEX UX_CustomerBanks_CustomerId_AccountNumber
        ON [dbo].[CustomerBanks]([CustomerId], [AccountNumber]);
END
GO
