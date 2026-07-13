-- =============================================
-- Drop unique organization/tenant name constraint
-- Same organization name may be used by multiple tenants.
-- Run against the catalog database (e.g. ezofis_catalog_Dev).
-- =============================================

PRINT '';
PRINT '=== Drop IX_Tenants_Name (allow duplicate organization names) ===';
PRINT '';

IF EXISTS (
    SELECT 1
    FROM sys.key_constraints kc
    INNER JOIN sys.tables t ON kc.parent_object_id = t.object_id
    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE s.name = N'catalog'
      AND t.name = N'Tenants'
      AND kc.name = N'IX_Tenants_Name'
)
BEGIN
    ALTER TABLE [catalog].[Tenants] DROP CONSTRAINT [IX_Tenants_Name];
    PRINT '✓ Dropped constraint IX_Tenants_Name';
END
ELSE IF EXISTS (
    SELECT 1
    FROM sys.indexes i
    INNER JOIN sys.tables t ON i.object_id = t.object_id
    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE s.name = N'catalog'
      AND t.name = N'Tenants'
      AND i.name = N'IX_Tenants_Name'
      AND i.is_unique = 1
)
BEGIN
    DROP INDEX [IX_Tenants_Name] ON [catalog].[Tenants];
    PRINT '✓ Dropped unique index IX_Tenants_Name';
END
ELSE
BEGIN
    PRINT '✓ IX_Tenants_Name already absent — nothing to do';
END
GO
