-- Add dms.StagingItems for existing tenant DBs (temp indexing before export)
-- Run on tenant database. New signups get this via CreateDmsSchema.sql.
-- Usage: sqlcmd -S server -d ezofis_Tenant_1 -U user -P pass -i AddDmsStagingItems.sql

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'dms')
    EXEC('CREATE SCHEMA dms');
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'StagingItems' AND schema_id = SCHEMA_ID('dms'))
BEGIN
    CREATE TABLE [dms].[StagingItems] (
        [Id] UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        [TenantId] UNIQUEIDENTIFIER NOT NULL,
        [RepositoryId] UNIQUEIDENTIFIER NOT NULL,
        [Year] SMALLINT NOT NULL,
        [InvoiceType] NVARCHAR(64) NOT NULL,
        [VendorName] NVARCHAR(256) NOT NULL,
        [FileName] NVARCHAR(512) NOT NULL,
        [FilePath] NVARCHAR(1024) NULL,
        [StoragePath] NVARCHAR(1024) NULL,
        [Status] TINYINT NOT NULL DEFAULT 0,
        [CreatedAt] DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        [CreatedBy] UNIQUEIDENTIFIER NOT NULL,
        [UpdatedAt] DATETIME2(3) NULL,
        [UpdatedBy] UNIQUEIDENTIFIER NULL,
        [ExportedAt] DATETIME2(3) NULL,
        [ExportedToItemId] UNIQUEIDENTIFIER NULL,
        INDEX [IX_StagingItems_Repository_Status] NONCLUSTERED ([RepositoryId], [Status]),
        INDEX [IX_StagingItems_CreatedBy] NONCLUSTERED ([CreatedBy]),
        INDEX [IX_StagingItems_CreatedAt] NONCLUSTERED ([CreatedAt] DESC)
    );
    PRINT 'DMS: StagingItems table created (temp indexing before export)';
END
ELSE
    PRINT 'DMS: StagingItems already exists';
GO
