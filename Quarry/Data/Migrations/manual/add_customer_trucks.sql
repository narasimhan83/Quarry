-- Add CustomerTrucks table for per-customer truck/plate registration.
-- Schema mirrors the design Tom shared (CustomerTruckId / CustomerTruckNumber /
-- IsActive / CreatedAt) plus a CustomerId FK so each truck is tied to exactly
-- one customer. A unique index on (CustomerId, CustomerTruckNumber) prevents
-- the same plate being registered twice against the same customer; two
-- different customers may still share a plate (rare, but legal).

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[CustomerTrucks]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[CustomerTrucks](
        [CustomerTruckId]     INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [CustomerId]          INT NOT NULL,
        [CustomerTruckNumber] NVARCHAR(100) NOT NULL,
        [IsActive]            BIT NOT NULL CONSTRAINT DF_CustomerTrucks_IsActive  DEFAULT (1),
        [CreatedAt]           DATETIME2(7) NOT NULL CONSTRAINT DF_CustomerTrucks_CreatedAt DEFAULT (SYSUTCDATETIME())
    );

    ALTER TABLE [dbo].[CustomerTrucks]
        ADD CONSTRAINT FK_CustomerTrucks_Customers_CustomerId
        FOREIGN KEY ([CustomerId])
        REFERENCES [dbo].[Customers]([Id])
        ON DELETE CASCADE;

    CREATE UNIQUE INDEX UX_CustomerTrucks_CustomerId_TruckNumber
        ON [dbo].[CustomerTrucks]([CustomerId], [CustomerTruckNumber]);
END
GO
