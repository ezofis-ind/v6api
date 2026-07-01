-- =============================================
-- CATALOG DATABASE - CREATE DATABASE ONLY
-- Run against master (or default connection)
-- =============================================

IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'ezofis_catalog_Dev')
BEGIN
    PRINT 'Creating catalog database: ezofis_catalog_Dev';
    CREATE DATABASE [ezofis_catalog_Dev];
    PRINT '✓ Catalog database created';
END
ELSE
BEGIN
    PRINT '✓ Catalog database already exists';
END
GO
