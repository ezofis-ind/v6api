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
        [AppVersion] NVARCHAR(32) NULL
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
-- ConnectorProviders (global OAuth apps — also created by 01a)
-- =============================================
IF OBJECT_ID(N'[catalog].[ConnectorProviders]', N'U') IS NULL
BEGIN
    CREATE TABLE [catalog].[ConnectorProviders] (
        [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_ConnectorProviders] PRIMARY KEY DEFAULT NEWID(),
        [ProviderCode] NVARCHAR(64) NOT NULL,
        [DisplayName] NVARCHAR(128) NOT NULL,
        [ClientId] NVARCHAR(512) NOT NULL CONSTRAINT [DF_ConnectorProviders_ClientId] DEFAULT (N''),
        [ClientSecret] NVARCHAR(1024) NOT NULL CONSTRAINT [DF_ConnectorProviders_ClientSecret] DEFAULT (N''),
        [AuthUrl] NVARCHAR(1024) NOT NULL,
        [TokenUrl] NVARCHAR(1024) NOT NULL,
        [Scopes] NVARCHAR(2000) NOT NULL CONSTRAINT [DF_ConnectorProviders_Scopes] DEFAULT (N''),
        [RedirectUri] NVARCHAR(1024) NOT NULL CONSTRAINT [DF_ConnectorProviders_RedirectUri] DEFAULT (N''),
        [ExtraConfigJson] NVARCHAR(MAX) NULL,
        [IsActive] BIT NOT NULL CONSTRAINT [DF_ConnectorProviders_IsActive] DEFAULT (1),
        [CreatedAtUtc] DATETIME2(3) NOT NULL CONSTRAINT [DF_ConnectorProviders_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
        [ModifiedAtUtc] DATETIME2(3) NULL,
        CONSTRAINT [UQ_ConnectorProviders_ProviderCode] UNIQUE ([ProviderCode])
    );
    PRINT '✓ catalog.ConnectorProviders created';
END
ELSE
BEGIN
    PRINT '✓ catalog.ConnectorProviders already exists';
END
GO

MERGE [catalog].[ConnectorProviders] AS t
USING (VALUES
    (N'GCP', N'Google Cloud Storage',
     N'https://accounts.google.com/o/oauth2/v2/auth',
     N'https://oauth2.googleapis.com/token',
     N'https://www.googleapis.com/auth/devstorage.read_write https://www.googleapis.com/auth/userinfo.email openid'),
    (N'GMAIL', N'Gmail',
     N'https://accounts.google.com/o/oauth2/v2/auth',
     N'https://oauth2.googleapis.com/token',
     N'https://www.googleapis.com/auth/gmail.modify https://www.googleapis.com/auth/userinfo.email openid'),
    (N'ONEDRIVE', N'Microsoft OneDrive',
     N'https://login.microsoftonline.com/common/oauth2/v2.0/authorize',
     N'https://login.microsoftonline.com/common/oauth2/v2.0/token',
     N'offline_access openid profile email Files.ReadWrite.All User.Read'),
    (N'TEAMS', N'Microsoft Teams',
     N'https://login.microsoftonline.com/common/oauth2/v2.0/authorize',
     N'https://login.microsoftonline.com/common/oauth2/v2.0/token',
     N'offline_access openid profile email Files.ReadWrite.All Sites.ReadWrite.All User.Read'),
    (N'DROPBOX', N'Dropbox',
     N'https://www.dropbox.com/oauth2/authorize',
     N'https://api.dropboxapi.com/oauth2/token',
     N''),
    (N'OUTLOOK', N'Office 365 Outlook',
     N'https://login.microsoftonline.com/common/oauth2/v2.0/authorize',
     N'https://login.microsoftonline.com/common/oauth2/v2.0/token',
     N'offline_access openid profile email Mail.ReadWrite User.Read'),
    (N'QUICKBOOKS', N'QuickBooks',
     N'https://appcenter.intuit.com/connect/oauth2',
     N'https://oauth.platform.intuit.com/oauth2/v1/tokens/bearer',
     N'com.intuit.quickbooks.accounting openid profile email')
) AS s ([ProviderCode], [DisplayName], [AuthUrl], [TokenUrl], [Scopes])
ON t.[ProviderCode] = s.[ProviderCode]
WHEN NOT MATCHED THEN
    INSERT ([Id], [ProviderCode], [DisplayName], [ClientId], [ClientSecret], [AuthUrl], [TokenUrl], [Scopes], [RedirectUri], [IsActive], [CreatedAtUtc])
    VALUES (NEWID(), s.[ProviderCode], s.[DisplayName], N'', N'', s.[AuthUrl], s.[TokenUrl], s.[Scopes], N'', 1, SYSUTCDATETIME());
GO
PRINT '✓ ConnectorProviders seed ensured';
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
