-- =========================================================================
-- Quarry Management System — Reconcile Customer OutstandingBalance
--
-- Customer outstanding balances can drift out of sync when weighments are
-- edited / deleted and the revert-and-reapply logic doesn't get a clean
-- pair of old-new values (most commonly when an earlier save had a wrong
-- TotalAmount, e.g. 0 because NetWeight was never persisted).
--
-- This script RECOMPUTES OutstandingBalance from scratch for every
-- customer, using the authoritative source: the sum of non-cancelled
-- invoice outstanding balances. Then it also clamps to 0 any residual
-- negatives that might remain for customers without invoices.
--
-- Formula:
--   OutstandingBalance = SUM(invoice.TotalAmount - invoice.PaidAmount)
--                        for that customer's invoices where Status != 'Cancelled'
--
-- Idempotent: safe to run multiple times. Running it when everything is
-- already correct is a no-op (same values computed, same values written).
--
-- When to run: any time the customer balance looks wrong (e.g. negative,
-- or doesn't match the sum of their open invoices).
-- =========================================================================

------------------------------------------------------------------------
-- 1. Recompute OutstandingBalance for every customer from their invoices.
--    Customers with no invoices end up at 0.
------------------------------------------------------------------------
UPDATE c
SET    c.OutstandingBalance = ISNULL(inv.Outstanding, 0)
FROM   dbo.Customers c
LEFT   JOIN (
    SELECT CustomerId,
           SUM(TotalAmount - ISNULL(PaidAmount, 0)) AS Outstanding
    FROM   dbo.Invoices
    WHERE  Status <> 'Cancelled'
    GROUP  BY CustomerId
) inv ON inv.CustomerId = c.Id;
GO

------------------------------------------------------------------------
-- 2. Floor at 0 as a final safety net. A customer who overpaid once
--    (prepayment accidentally posted against AR) could land at negative;
--    we treat that as 0 outstanding because the surplus belongs in the
--    prepayment wallet, not in AR.
------------------------------------------------------------------------
UPDATE dbo.Customers
SET    OutstandingBalance = 0
WHERE  OutstandingBalance < 0;
GO

PRINT 'Customer OutstandingBalance reconciliation complete.';
GO
