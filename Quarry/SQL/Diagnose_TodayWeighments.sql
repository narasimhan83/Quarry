-- =========================================================================
-- Quarry Management System — Diagnostic: why does the dashboard show only
-- 1 weighment for today when more were created?
--
-- Run this in SSMS to see the exact TransactionDate values and compare them
-- to what the server considers "today". Nothing is modified.
-- =========================================================================

DECLARE @today DATE = CAST(GETDATE() AS DATE);

PRINT 'Server current time:   ' + CONVERT(NVARCHAR(30), GETDATE(), 120);
PRINT 'Server "today" (DATE):  ' + CONVERT(NVARCHAR(10), @today, 120);
PRINT '';

------------------------------------------------------------------------
-- 1. All weighments in the last 7 days, with their TransactionDate.
--    This is the dashboard's source of truth.
------------------------------------------------------------------------
SELECT
    Id,
    TransactionNumber,
    TransactionDate,
    CAST(TransactionDate AS DATE)               AS TransactionDay,
    CASE WHEN CAST(TransactionDate AS DATE) = @today
         THEN 'YES' ELSE 'no' END               AS CountedInTodayKpi,
    Status,
    TotalAmount,
    CreatedAt,
    CreatedBy
FROM   dbo.WeighmentTransactions
WHERE  TransactionDate >= DATEADD(DAY, -7, @today)
ORDER  BY TransactionDate DESC, Id DESC;

------------------------------------------------------------------------
-- 2. Summary: count of weighments grouped by TransactionDate day.
--    If you see multiple rows here with recent dates and only one
--    matches @today, that confirms the dashboard is counting correctly
--    — the other weighments just have a different date stamp.
------------------------------------------------------------------------
SELECT
    CAST(TransactionDate AS DATE) AS TransactionDay,
    COUNT(*)                      AS WeighmentCount,
    MIN(TransactionDate)          AS EarliestTime,
    MAX(TransactionDate)          AS LatestTime
FROM   dbo.WeighmentTransactions
WHERE  TransactionDate >= DATEADD(DAY, -7, @today)
GROUP  BY CAST(TransactionDate AS DATE)
ORDER  BY TransactionDay DESC;
