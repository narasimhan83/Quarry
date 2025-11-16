-- Fix database constraint issues
-- This script removes the problematic foreign key constraint that references the non-existent Vehicles table

-- Check if the foreign key exists and drop it
IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK__Weighment__Vehic__628FA481')
BEGIN
    ALTER TABLE WeighmentTransactions DROP CONSTRAINT FK__Weighment__Vehic__628FA481;
    PRINT 'Foreign key constraint FK__Weighment__Vehic__628FA481 dropped successfully';
END
ELSE
BEGIN
    PRINT 'Foreign key constraint FK__Weighment__Vehic__628FA481 does not exist';
END

-- Ensure VehicleRegNumber is a simple string field without foreign key constraints
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('WeighmentTransactions') AND name = 'VehicleRegNumber')
BEGIN
    PRINT 'VehicleRegNumber column exists in WeighmentTransactions table';
END
ELSE
BEGIN
    PRINT 'VehicleRegNumber column does not exist - this needs to be created';
END

-- Check for any other problematic foreign keys
SELECT 
    fk.name AS ForeignKeyName,
    tp.name AS ParentTable,
    tr.name AS ReferencedTable,
    cp.name AS ParentColumn,
    cr.name AS ReferencedColumn
FROM sys.foreign_keys fk
INNER JOIN sys.tables tp ON fk.parent_object_id = tp.object_id
INNER JOIN sys.tables tr ON fk.referenced_object_id = tr.object_id
INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
INNER JOIN sys.columns cp ON fkc.parent_object_id = cp.object_id AND fkc.parent_column_id = cp.column_id
INNER JOIN sys.columns cr ON fkc.referenced_object_id = cr.object_id AND fkc.referenced_column_id = cr.column_id
WHERE tr.name = 'Vehicles' OR tp.name = 'WeighmentTransactions';

PRINT 'Database constraint check completed';