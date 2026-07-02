-- =============================================
-- CATALOG DATABASE - CREATE TABLES
-- Run against ezofis_catalog_Dev
-- Usage: sqlcmd -S localhost -d ezofis_catalog_Dev -i 01b_CreateCatalogTables.sql
-- =============================================

PRINT '';
PRINT '=== Creating Catalog Tables ===';
PRINT '';

-- =============================================
-- Catalog Schema (required by EF - catalog.Tenants, catalog.UserTenants)
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'catalog')
BEGIN
    EXEC('CREATE SCHEMA [catalog]');
    PRINT '✓ catalog schema created';
END
ELSE
BEGIN
    PRINT '✓ catalog schema already exists';
END
GO

-- =============================================
-- Tenants Table (catalog.Tenants - matches EF default schema)
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables t INNER JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'Tenants' AND s.name = 'catalog')
BEGIN
    CREATE TABLE [catalog].[Tenants] (
        [Id] UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        [Name] NVARCHAR(256) NOT NULL,
        [ConnectionString] NVARCHAR(1024) NOT NULL,
        [IsActive] BIT NOT NULL DEFAULT 1,
        [CreatedAtUtc] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [ModifiedAtUtc] DATETIME2 NULL,
        [Email] NVARCHAR(256) NULL,
        [SignupSource] NVARCHAR(128) NULL,
        [Platform] NVARCHAR(64) NULL,
        [AppVersion] NVARCHAR(32) NULL,
        CONSTRAINT [IX_Tenants_Name] UNIQUE ([Name])
    );
    CREATE INDEX [IX_Tenants_IsActive] ON [catalog].[Tenants] ([IsActive]);
    PRINT '✓ catalog.Tenants table created';
END
ELSE
BEGIN
    PRINT '✓ catalog.Tenants table already exists';
END
GO

-- =============================================
-- UserTenants Table (catalog.UserTenants - multi-tenant user mapping)
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables t INNER JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'UserTenants' AND s.name = 'catalog')
BEGIN
    CREATE TABLE [catalog].[UserTenants] (
        [Id] UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        [Email] NVARCHAR(256) NOT NULL,
        [TenantId] UNIQUEIDENTIFIER NOT NULL,
        [Role] NVARCHAR(64) NOT NULL,
        [IsSuperuser] BIT NOT NULL DEFAULT 0,
        [CreatedAtUtc] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [ModifiedAtUtc] DATETIME2 NULL,
        CONSTRAINT [FK_UserTenants_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [catalog].[Tenants]([Id]) ON DELETE CASCADE,
        CONSTRAINT [IX_UserTenants_Email_TenantId] UNIQUE ([Email], [TenantId])
    );
    CREATE INDEX [IX_UserTenants_TenantId] ON [catalog].[UserTenants] ([TenantId]);
    PRINT '✓ catalog.UserTenants table created';
END
ELSE
BEGIN
    PRINT '✓ catalog.UserTenants table already exists';
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('catalog.UserTenants') AND name = 'IsSuperuser')
    BEGIN
        ALTER TABLE [catalog].[UserTenants] ADD [IsSuperuser] BIT NOT NULL DEFAULT 0;
        PRINT '✓ IsSuperuser column added to catalog.UserTenants';
    END
END
GO

-- =============================================
-- EF Migrations History (dbo - EF default for migrations table)
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = '__EFMigrationsHistory')
BEGIN
    CREATE TABLE [dbo].[__EFMigrationsHistory] (
        [MigrationId] NVARCHAR(150) PRIMARY KEY,
        [ProductVersion] NVARCHAR(32) NOT NULL
    );
    PRINT '✓ EF Migrations History table created';
    
    INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES ('20260226000000_InitialCatalog', '8.0.0');
    PRINT '✓ Initial migration entry added';
END
ELSE
BEGIN
    PRINT '✓ EF Migrations History table already exists';
END
GO

-- =============================================
-- Repository item shares (cross-tenant file share links)
-- =============================================
IF NOT EXISTS (
    SELECT 1 FROM sys.tables t
    INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
    WHERE s.name = N'catalog' AND t.name = N'RepositoryItemShares')
BEGIN
    CREATE TABLE catalog.RepositoryItemShares (
        Id                  UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_RepositoryItemShares PRIMARY KEY DEFAULT NEWID(),
        ShareToken          NVARCHAR(128)    NOT NULL,
        SourceTenantId      UNIQUEIDENTIFIER NOT NULL,
        SourceRepositoryId  UNIQUEIDENTIFIER NOT NULL,
        SourceItemId        UNIQUEIDENTIFIER NOT NULL,
        SharedByUserId      UNIQUEIDENTIFIER NOT NULL,
        RecipientEmail      NVARCHAR(256)    NOT NULL,
        Message             NVARCHAR(2000)   NULL,
        Status              NVARCHAR(32)     NOT NULL CONSTRAINT DF_RepositoryItemShares_Status DEFAULT N'Active',
        ExpiresAtUtc        DATETIME2        NOT NULL,
        CreatedAtUtc        DATETIME2        NOT NULL CONSTRAINT DF_RepositoryItemShares_CreatedAt DEFAULT SYSUTCDATETIME(),
        LastAccessedAtUtc   DATETIME2        NULL
    );

    CREATE UNIQUE INDEX IX_RepositoryItemShares_ShareToken
        ON catalog.RepositoryItemShares (ShareToken);

    CREATE INDEX IX_RepositoryItemShares_Recipient_Status
        ON catalog.RepositoryItemShares (RecipientEmail, Status);

    CREATE INDEX IX_RepositoryItemShares_Source
        ON catalog.RepositoryItemShares (SourceTenantId, SourceRepositoryId, SourceItemId);

    PRINT '✓ catalog.RepositoryItemShares created';
END
ELSE
    PRINT '✓ catalog.RepositoryItemShares already exists';
GO

PRINT '';
PRINT '=== Catalog Database Setup Complete ===';
PRINT '';
SELECT name AS TableName, create_date AS CreatedDate
FROM sys.tables
WHERE is_ms_shipped = 0
ORDER BY name;
PRINT '';
PRINT '✓ Catalog database is ready!';
GO
