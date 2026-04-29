-- =========================================================================
-- Quarry Management System — Backfill WeighmentTransactions.NetWeight
--
-- Fixes legacy rows where NetWeight stayed at 0 because the old controller
-- never assigned it on save. From now on, ApplyVatTreatmentAsync in
-- WeighmentController.cs computes and persists NetWeight = Gross − Tare on
-- every Create/Edit, so only historical rows need patching.
--
-- Also nudges SubTotal / VatAmount / TotalAmount toward something reasonable
-- for the simplest (Exclusive, no rebate, no kg unit) case. Rows with
-- customer-specific VAT treatment or rebates won't be perfect here — the
-- operator can open each affected weighment and hit Save to get the
-- authoritative recompute from the controller. But this gets the list view
-- usable immediately without requiring a per-row manual pass.
--
-- Idempotent: every statement is guarded by a WHERE clause so reruns only
-- touch rows that still need fixing.
-- =========================================================================

------------------------------------------------------------------------
-- 1. NetWeight = Gross − Tare for every row where the stored value is stale.
--    "Stale" = NetWeight is 0 or NULL while Gross > Tare. Legitimate
--    Tare-only first weighings (Gross = 0) correctly stay at NetWeight = 0.
------------------------------------------------------------------------
UPDATE dbo.WeighmentTransactions
SET    NetWeight = GrossWeight - ISNULL(TareWeight, 0)
WHERE  GrossWeight > ISNULL(TareWeight, 0)
  AND  (NetWeight IS NULL OR NetWeight = 0
        OR NetWeight <> (GrossWeight - ISNULL(TareWeight, 0)));
GO

------------------------------------------------------------------------
-- 2. Recompute Subtotal / VAT / Total for the simple case where they're
--    missing or inconsistent. Only touches rows where:
--      - We now have a valid NetWeight > 0
--      - PricePerUnit is populated
--      - Subtotal is NULL or 0 (avoid overwriting rows that were saved
--        correctly under a different VAT treatment or rebate)
--
--    Uses plain Exclusive VAT math and assumes the weight unit is Ton
--    (the current default). Rows with WeightUnit = 'kg' are skipped to
--    avoid a unit-conversion surprise; those should be opened and
--    re-saved through the form so the controller handles them properly.
------------------------------------------------------------------------
UPDATE dbo.WeighmentTransactions
SET    SubTotal    = ROUND(NetWeight * PricePerUnit, 2),
       VatAmount   = ROUND(NetWeight * PricePerUnit * (VatRate / 100.0), 2),
       RebateAmount = ISNULL(RebateAmount, 0),
       TotalAmount = ROUND(
                        NetWeight * PricePerUnit
                        + NetWeight * PricePerUnit * (VatRate / 100.0)
                        - ISNULL(RebateAmount, 0),
                        2)
WHERE  NetWeight > 0
  AND  PricePerUnit IS NOT NULL
  AND  PricePerUnit > 0
  AND  (WeightUnit IS NULL OR WeightUnit <> 'kg')
  AND  (SubTotal IS NULL OR SubTotal = 0);
GO

PRINT 'WeighmentTransactions NetWeight / financial backfill complete.';
GO
