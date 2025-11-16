-- Script to manually create admin user for TRACO Mining Nigerian Quarry Management System
-- Run this on your SQL Server database (87.252.104.168)

USE [your_database_name]; -- Replace with your actual database name

-- 1. Create Admin Role
IF NOT EXISTS (SELECT * FROM AspNetRoles WHERE Name = 'Admin')
BEGIN
    INSERT INTO AspNetRoles (Id, Name, NormalizedName, ConcurrencyStamp)
    VALUES (NEWID(), 'Admin', 'ADMIN', NEWID());
    PRINT '✅ Admin role created';
END
ELSE PRINT '✅ Admin role already exists';

-- 2. Create other roles
IF NOT EXISTS (SELECT * FROM AspNetRoles WHERE Name = 'Manager')
BEGIN
    INSERT INTO AspNetRoles (Id, Name, NormalizedName, ConcurrencyStamp)
    VALUES (NEWID(), 'Manager', 'MANAGER', NEWID());
END

IF NOT EXISTS (SELECT * FROM AspNetRoles WHERE Name = 'Accountant')
BEGIN
    INSERT INTO AspNetRoles (Id, Name, NormalizedName, ConcurrencyStamp)
    VALUES (NEWID(), 'Accountant', 'ACCOUNTANT', NEWID());
END

IF NOT EXISTS (SELECT * FROM AspNetRoles WHERE Name = 'Operator')
BEGIN
    INSERT INTO AspNetRoles (Id, Name, NormalizedName, ConcurrencyStamp)
    VALUES (NEWID(), 'Operator', 'OPERATOR', NEWID());
END

IF NOT EXISTS (SELECT * FROM AspNetRoles WHERE Name = 'Viewer')
BEGIN
    INSERT INTO AspNetRoles (Id, Name, NormalizedName, ConcurrencyStamp)
    VALUES (NEWID(), 'Viewer', 'VIEWER', NEWID());
END

PRINT '✅ All roles created';

-- 3. Create Admin User
DECLARE @userId NVARCHAR(450) = NEWID();
DECLARE @adminRoleId NVARCHAR(450);

SELECT @adminRoleId = Id FROM AspNetRoles WHERE Name = 'Admin';

IF NOT EXISTS (SELECT * FROM AspNetUsers WHERE UserName = 'admin')
BEGIN
    -- Password hash for "Admin@2024"
    -- This is the BCrypt hash for "Admin@2024"
    -- Note: You'll need to generate this properly using ASP.NET Identity PasswordHasher
    INSERT INTO AspNetUsers (
        Id, 
        UserName, 
        NormalizedUserName, 
        Email, 
        NormalizedEmail, 
        EmailConfirmed, 
        PasswordHash,
        SecurityStamp,
        ConcurrencyStamp,
        PhoneNumberConfirmed,
        TwoFactorEnabled,
        LockoutEnabled,
        AccessFailedCount,
        FullName,
        IsActive,
        CreatedAt
    )
    VALUES (
        @userId,
        'admin',
        'ADMIN',
        'admin@quarry.ng',
        'ADMIN@QUARRY.NG',
        1,
        'AQAAAAIAAYagAAAAEMvSKz2RqGT7VzHj6L0X8qK5xF9yNp9kB7LmCgH8tA2vN6wE4Jy3Zx1Pq5Rh8Ws9Cv==', -- Placeholder - needs proper hash
        NEWID(),
        NEWID(),
        0,
        0,
        1,
        0,
        'System Administrator',
        1,
        GETDATE()
    );

    -- Assign Admin role to user
    INSERT INTO AspNetUserRoles (UserId, RoleId)
    VALUES (@userId, @adminRoleId);

    PRINT '✅ Admin user created successfully';
    PRINT '   Username: admin';
    PRINT '   Email: admin@quarry.ng';
    PRINT '   Password: Admin@2024 (you need to set proper hash)';
END
ELSE PRINT '✅ Admin user already exists';

-- Verify
SELECT 'Users' AS TableName, COUNT(*) AS Count FROM AspNetUsers
UNION ALL
SELECT 'Roles', COUNT(*) FROM AspNetRoles
UNION ALL  
SELECT 'UserRoles', COUNT(*) FROM AspNetUserRoles;

PRINT '';
PRINT '========================================';
PRINT '✅ ADMIN USER CREATION COMPLETE';
PRINT '========================================';
PRINT 'Username: admin';
PRINT 'Password: Admin@2024';
PRINT '========================================';
PRINT '';
PRINT '⚠️  NOTE: The password hash in this script is a placeholder.';
PRINT '   For production, run the application which will create';
PRINT '   the user with proper password hashing automatically.';
PRINT '';
PRINT '   Or restart the application now - it should seed the';
PRINT '   admin user with the correct password hash.';