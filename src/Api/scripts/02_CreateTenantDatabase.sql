-- =============================================
-- TENANT DATABASE - COMPLETE SETUP
-- Run this for EACH new tenant database
-- Creates: Users schema + Workflow schema (7 core tables)
-- =============================================

-- STEP 1: Change this to your tenant database name
DECLARE @TenantDatabaseName NVARCHAR(128) = 'ezofis_Tenant_1';  -- CHANGE THIS!

-- =============================================
-- Create Database
-- =============================================
USE [master]
GO

DECLARE @SQL NVARCHAR(MAX);

IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = @TenantDatabaseName)
BEGIN
    PRINT 'Creating tenant database: ' + @TenantDatabaseName;
    SET @SQL = 'CREATE DATABASE [' + @TenantDatabaseName + ']';
    EXEC sp_executesql @SQL;
    PRINT '✓ Tenant database created';
END
ELSE
BEGIN
    PRINT '✓ Tenant database already exists: ' + @TenantDatabaseName;
END
GO

-- Switch to tenant database
DECLARE @TenantDatabaseName NVARCHAR(128) = 'ezofis_Tenant_1';  -- CHANGE THIS!
DECLARE @UseSQL NVARCHAR(MAX) = 'USE [' + @TenantDatabaseName + ']';
EXEC sp_executesql @UseSQL;
GO

-- For the rest of the script, manually change the USE statement
USE [ezofis_Tenant_1]  -- CHANGE THIS!
GO

PRINT '';
PRINT '=== Setting up tenant database: ' + DB_NAME() + ' ===';
PRINT '';

-- =============================================
-- PART 1: USERS SCHEMA
-- =============================================

PRINT '=== Creating Users Schema ===';
PRINT '';

-- Create users schema
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'users')
BEGIN
    EXEC('CREATE SCHEMA users');
    PRINT '✓ Users schema created';
END
ELSE
BEGIN
    PRINT '✓ Users schema already exists';
END
GO

-- Users table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Users' AND schema_id = SCHEMA_ID('users'))
BEGIN
    CREATE TABLE [users].[Users] (
        [Id] UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        [TenantId] UNIQUEIDENTIFIER NOT NULL,
        [Email] NVARCHAR(256) NOT NULL,
        [DisplayName] NVARCHAR(256) NOT NULL,
        [Role] NVARCHAR(64) NOT NULL,
        [AuthStrategy] NVARCHAR(64) NOT NULL,
        [PasswordHash] NVARCHAR(512) NULL,
        [TotpSecret] NVARCHAR(256) NULL,
        [IsTotpEnabled] BIT NOT NULL DEFAULT 0,
        [FirstName] NVARCHAR(128) NULL,
        [LastName] NVARCHAR(128) NULL,
        [PhoneNumber] NVARCHAR(32) NULL,
        [Department] NVARCHAR(128) NULL,
        [JobTitle] NVARCHAR(128) NULL,
        [ProfilePictureUrl] NVARCHAR(512) NULL,
        [IsActive] BIT NOT NULL DEFAULT 1,
        [LastLoginAtUtc] DATETIME2 NULL,
        [CreatedAtUtc] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [ModifiedAtUtc] DATETIME2 NULL,
        CONSTRAINT [IX_Users_TenantId_Email] UNIQUE NONCLUSTERED ([TenantId], [Email]),
        CONSTRAINT [IX_Users_TenantId_IsActive] NONCLUSTERED ([TenantId], [IsActive])
    );
    PRINT '✓ Users table created';
END
ELSE
BEGIN
    PRINT '✓ Users table already exists';
END
GO

-- EF Migrations History for Users
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = '__EFMigrationsHistory' AND schema_id = SCHEMA_ID('users'))
BEGIN
    CREATE TABLE [users].[__EFMigrationsHistory] (
        [MigrationId] NVARCHAR(150) PRIMARY KEY,
        [ProductVersion] NVARCHAR(32) NOT NULL
    );
    PRINT '✓ Users EF Migrations History created';
    
    INSERT INTO [users].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES ('20250226000000_InitialUsers', '8.0.0');
    PRINT '✓ Initial users migration entry added';
END
ELSE
BEGIN
    PRINT '✓ Users EF Migrations History already exists';
END
GO

-- =============================================
-- PART 2: WORKFLOW SCHEMA (7 CORE TABLES)
-- =============================================

PRINT '';
PRINT '=== Creating Workflow Schema ===';
PRINT '';

-- Create workflow schema
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'workflow')
BEGIN
    EXEC('CREATE SCHEMA workflow');
    PRINT '✓ Workflow schema created';
END
ELSE
BEGIN
    PRINT '✓ Workflow schema already exists';
END
GO

-- 1. Workflows table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Workflows' AND schema_id = SCHEMA_ID('workflow'))
BEGIN
    CREATE TABLE [workflow].[Workflows] (
        [Id] UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        [TenantId] UNIQUEIDENTIFIER NOT NULL,
        [Name] NVARCHAR(256) NOT NULL,
        [Description] NVARCHAR(2000) NULL,
        [Status] INT NOT NULL DEFAULT 0,
        [TriggerType] INT NOT NULL DEFAULT 0,
        [TriggerConfig] NVARCHAR(4000) NULL,
        [Version] INT NOT NULL DEFAULT 1,
        [CreatedAtUtc] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [ModifiedAtUtc] DATETIME2 NULL,
        [CreatedBy] UNIQUEIDENTIFIER NOT NULL,
        [ModifiedBy] UNIQUEIDENTIFIER NULL,
        [IsDeleted] BIT NOT NULL DEFAULT 0,
        CONSTRAINT [IX_Workflows_TenantId_IsDeleted] NONCLUSTERED ([TenantId], [IsDeleted])
    );
    PRINT '✓ Workflows table created';
END
ELSE
BEGIN
    PRINT '✓ Workflows table already exists';
END
GO

-- 2. WorkflowSteps table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'WorkflowSteps' AND schema_id = SCHEMA_ID('workflow'))
BEGIN
    CREATE TABLE [workflow].[WorkflowSteps] (
        [Id] UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        [WorkflowId] UNIQUEIDENTIFIER NOT NULL,
        [Name] NVARCHAR(256) NOT NULL,
        [Description] NVARCHAR(2000) NULL,
        [StepType] INT NOT NULL DEFAULT 0,
        [Order] INT NOT NULL,
        [Config] NVARCHAR(4000) NULL,
        [IsRequired] BIT NOT NULL DEFAULT 1,
        [AssignedToUserId] UNIQUEIDENTIFIER NULL,
        [AssignedToRole] NVARCHAR(64) NULL,
        [ActivityId] NVARCHAR(128) NULL,
        [CreatedAtUtc] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT [FK_WorkflowSteps_Workflow] FOREIGN KEY ([WorkflowId]) REFERENCES [workflow].[Workflows]([Id]) ON DELETE CASCADE,
        CONSTRAINT [IX_WorkflowSteps_WorkflowId_Order] NONCLUSTERED ([WorkflowId], [Order])
    );
    PRINT '✓ WorkflowSteps table created';
END
ELSE
BEGIN
    PRINT '✓ WorkflowSteps table already exists';
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[workflow].[WorkflowSteps]') AND name = 'ActivityId')
    BEGIN
        ALTER TABLE [workflow].[WorkflowSteps] ADD [ActivityId] NVARCHAR(128) NULL;
        PRINT '✓ WorkflowSteps.ActivityId column added';
    END
END
GO

-- 3. WorkflowInstances table (with all extended fields)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'WorkflowInstances' AND schema_id = SCHEMA_ID('workflow'))
BEGIN
    CREATE TABLE [workflow].[WorkflowInstances] (
        [Id] UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        [TenantId] UNIQUEIDENTIFIER NOT NULL,
        [WorkflowId] UNIQUEIDENTIFIER NOT NULL,
        [WorkflowName] NVARCHAR(256) NOT NULL,
        [WorkflowVersion] INT NOT NULL,
        [Status] INT NOT NULL DEFAULT 0,
        [CurrentStepInstanceId] UNIQUEIDENTIFIER NULL,
        [CreatedAtUtc] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [StartedAtUtc] DATETIME2 NULL,
        [CompletedAtUtc] DATETIME2 NULL,
        [StartedBy] UNIQUEIDENTIFIER NOT NULL,
        [Context] NVARCHAR(4000) NULL,
        [ErrorMessage] NVARCHAR(2000) NULL,
        -- Extended fields
        [ReferenceNumber] NVARCHAR(128) NULL,
        [CustomerName] NVARCHAR(256) NULL,
        [CustomerEmail] NVARCHAR(256) NULL,
        [CustomerPhone] NVARCHAR(64) NULL,
        [Department] NVARCHAR(128) NULL,
        [Category] NVARCHAR(128) NULL,
        [Priority] INT NOT NULL DEFAULT 1,
        [Tags] NVARCHAR(1000) NULL,
        [CustomFieldsJson] NVARCHAR(4000) NULL,
        [AssignedToUserId] UNIQUEIDENTIFIER NULL,
        [AssignedToGroupId] UNIQUEIDENTIFIER NULL,
        [LastActivityAtUtc] DATETIME2 NULL,
        [ViewCount] INT NOT NULL DEFAULT 0,
        [IsArchived] BIT NOT NULL DEFAULT 0,
        [ArchivedAtUtc] DATETIME2 NULL,
        [SourceType] NVARCHAR(64) NULL,
        [SourceId] NVARCHAR(256) NULL,
        CONSTRAINT [IX_WorkflowInstances_TenantId_WorkflowId] NONCLUSTERED ([TenantId], [WorkflowId]),
        CONSTRAINT [IX_WorkflowInstances_TenantId_Status_IsArchived] NONCLUSTERED ([TenantId], [Status], [IsArchived]),
        CONSTRAINT [IX_WorkflowInstances_ReferenceNumber] NONCLUSTERED ([ReferenceNumber]),
        CONSTRAINT [IX_WorkflowInstances_CustomerEmail] NONCLUSTERED ([CustomerEmail])
    );
    PRINT '✓ WorkflowInstances table created';
END
ELSE
BEGIN
    PRINT '✓ WorkflowInstances table already exists';
END
GO

-- 4. WorkflowStepInstances table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'WorkflowStepInstances' AND schema_id = SCHEMA_ID('workflow'))
BEGIN
    CREATE TABLE [workflow].[WorkflowStepInstances] (
        [Id] UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        [WorkflowInstanceId] UNIQUEIDENTIFIER NOT NULL,
        [WorkflowStepId] UNIQUEIDENTIFIER NOT NULL,
        [StepName] NVARCHAR(256) NOT NULL,
        [StepType] INT NOT NULL,
        [Order] INT NOT NULL,
        [Status] INT NOT NULL DEFAULT 0,
        [AssignedToUserId] UNIQUEIDENTIFIER NULL,
        [AssignedToRole] NVARCHAR(64) NULL,
        [CreatedAtUtc] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [StartedAtUtc] DATETIME2 NULL,
        [CompletedAtUtc] DATETIME2 NULL,
        [CompletedBy] UNIQUEIDENTIFIER NULL,
        [Result] NVARCHAR(4000) NULL,
        [ErrorMessage] NVARCHAR(2000) NULL,
        CONSTRAINT [FK_WorkflowStepInstances_WorkflowInstance] FOREIGN KEY ([WorkflowInstanceId]) REFERENCES [workflow].[WorkflowInstances]([Id]) ON DELETE CASCADE,
        CONSTRAINT [IX_WorkflowStepInstances_WorkflowInstanceId_Order] NONCLUSTERED ([WorkflowInstanceId], [Order])
    );
    PRINT '✓ WorkflowStepInstances table created';
END
ELSE
BEGIN
    PRINT '✓ WorkflowStepInstances table already exists';
END
GO

-- 5. WorkflowApprovals table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'WorkflowApprovals' AND schema_id = SCHEMA_ID('workflow'))
BEGIN
    CREATE TABLE [workflow].[WorkflowApprovals] (
        [Id] UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        [TenantId] UNIQUEIDENTIFIER NOT NULL,
        [WorkflowInstanceId] UNIQUEIDENTIFIER NOT NULL,
        [StepInstanceId] UNIQUEIDENTIFIER NOT NULL,
        [RequestedBy] UNIQUEIDENTIFIER NOT NULL,
        [AssignedToUserId] UNIQUEIDENTIFIER NULL,
        [AssignedToRole] NVARCHAR(64) NULL,
        [Status] INT NOT NULL DEFAULT 0,
        [CreatedAtUtc] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [RespondedAtUtc] DATETIME2 NULL,
        [RespondedBy] UNIQUEIDENTIFIER NULL,
        [Comments] NVARCHAR(2000) NULL,
        CONSTRAINT [IX_WorkflowApprovals_TenantId_AssignedToUserId_Status] NONCLUSTERED ([TenantId], [AssignedToUserId], [Status])
    );
    PRINT '✓ WorkflowApprovals table created';
END
ELSE
BEGIN
    PRINT '✓ WorkflowApprovals table already exists';
END
GO

-- 6. WorkflowSlas table (SLA policies)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'WorkflowSlas' AND schema_id = SCHEMA_ID('workflow'))
BEGIN
    CREATE TABLE [workflow].[WorkflowSlas] (
        [Id] UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        [TenantId] UNIQUEIDENTIFIER NOT NULL,
        [WorkflowId] UNIQUEIDENTIFIER NOT NULL,
        [Priority] INT NOT NULL DEFAULT 1,
        [ResponseTimeMinutes] INT NOT NULL,
        [ResolutionTimeMinutes] INT NOT NULL,
        [EscalationTimeMinutes] INT NULL,
        [EscalateToUserId] UNIQUEIDENTIFIER NULL,
        [EscalateToRole] NVARCHAR(64) NULL,
        [SendNotificationOnBreach] BIT NOT NULL DEFAULT 1,
        [NotificationEmails] NVARCHAR(1000) NULL,
        [CreatedAtUtc] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [ModifiedAtUtc] DATETIME2 NULL,
        CONSTRAINT [FK_WorkflowSlas_Workflow] FOREIGN KEY ([WorkflowId]) REFERENCES [workflow].[Workflows]([Id]) ON DELETE CASCADE,
        CONSTRAINT [UQ_WorkflowSlas_WorkflowId] UNIQUE ([WorkflowId])
    );
    PRINT '✓ WorkflowSlas table created';
END
ELSE
BEGIN
    PRINT '✓ WorkflowSlas table already exists';
END
GO

-- 7. WorkflowInstanceSlas table (SLA tracking)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'WorkflowInstanceSlas' AND schema_id = SCHEMA_ID('workflow'))
BEGIN
    CREATE TABLE [workflow].[WorkflowInstanceSlas] (
        [Id] UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        [WorkflowInstanceId] UNIQUEIDENTIFIER NOT NULL,
        [Priority] INT NOT NULL,
        [ResponseDeadline] DATETIME2 NOT NULL,
        [ResolutionDeadline] DATETIME2 NOT NULL,
        [EscalationDeadline] DATETIME2 NULL,
        [ResponseAchievedAt] DATETIME2 NULL,
        [ResolutionAchievedAt] DATETIME2 NULL,
        [ResponseStatus] INT NOT NULL DEFAULT 0,
        [ResolutionStatus] INT NOT NULL DEFAULT 0,
        [IsEscalated] BIT NOT NULL DEFAULT 0,
        [EscalatedAt] DATETIME2 NULL,
        [CreatedAtUtc] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT [FK_WorkflowInstanceSlas_WorkflowInstance] FOREIGN KEY ([WorkflowInstanceId]) REFERENCES [workflow].[WorkflowInstances]([Id]) ON DELETE CASCADE,
        CONSTRAINT [UQ_WorkflowInstanceSlas_WorkflowInstanceId] UNIQUE ([WorkflowInstanceId]),
        CONSTRAINT [IX_WorkflowInstanceSlas_ResponseStatus_ResolutionStatus] NONCLUSTERED ([ResponseStatus], [ResolutionStatus])
    );
    PRINT '✓ WorkflowInstanceSlas table created';
END
ELSE
BEGIN
    PRINT '✓ WorkflowInstanceSlas table already exists';
END
GO

-- =============================================
-- PART 3: TEMPORAL TABLES (Audit History)
-- =============================================

PRINT '';
PRINT '=== Enabling Temporal Tables (Audit History) ===';
PRINT '';

-- WorkflowInstances temporal table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('workflow.WorkflowInstances') AND name = 'SysStartTime')
BEGIN
    PRINT 'Adding temporal columns to WorkflowInstances...';
    ALTER TABLE [workflow].[WorkflowInstances] ADD 
        [SysStartTime] DATETIME2 GENERATED ALWAYS AS ROW START NOT NULL DEFAULT SYSUTCDATETIME(),
        [SysEndTime] DATETIME2 GENERATED ALWAYS AS ROW END NOT NULL DEFAULT CAST('9999-12-31 23:59:59.9999999' AS DATETIME2),
        PERIOD FOR SYSTEM_TIME ([SysStartTime], [SysEndTime]);
    PRINT '✓ Temporal columns added';
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'WorkflowInstances' AND schema_id = SCHEMA_ID('workflow') AND temporal_type = 2)
BEGIN
    PRINT 'Enabling system versioning for WorkflowInstances...';
    ALTER TABLE [workflow].[WorkflowInstances] SET (SYSTEM_VERSIONING = ON (HISTORY_TABLE = [workflow].[WorkflowInstancesHistory]));
    PRINT '✓ System versioning enabled for WorkflowInstances';
END
ELSE
BEGIN
    PRINT '✓ WorkflowInstances temporal table already enabled';
END
GO

-- =============================================
-- PART 4: DMS SCHEMA (Document Management - Folder Structure)
-- =============================================

PRINT '';
PRINT '=== Creating DMS Schema ===';
PRINT '';

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'dms')
BEGIN
    EXEC('CREATE SCHEMA dms');
    PRINT 'DMS schema created';
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Repository' AND schema_id = SCHEMA_ID('dms'))
BEGIN
    CREATE TABLE [dms].[Repository] (
        [Id] UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        [TenantId] UNIQUEIDENTIFIER NOT NULL,
        [Code] NVARCHAR(32) NOT NULL,
        [Name] NVARCHAR(256) NOT NULL,
        [Description] NVARCHAR(2000) NULL,
        [ItemsTableName] NVARCHAR(128) NOT NULL,
        [CreatedAtUtc] DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        [ModifiedAtUtc] DATETIME2(3) NULL,
        CONSTRAINT [UQ_Repository_TenantId_Code] UNIQUE ([TenantId], [Code]),
        INDEX [IX_Repository_TenantId] NONCLUSTERED ([TenantId])
    );
    PRINT 'DMS: Repository table created';
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'RepositoryFolderConfig' AND schema_id = SCHEMA_ID('dms'))
BEGIN
    CREATE TABLE [dms].[RepositoryFolderConfig] (
        [RepositoryId] UNIQUEIDENTIFIER NOT NULL,
        [LevelOrder] TINYINT NOT NULL,
        [FieldName] NVARCHAR(64) NOT NULL,
        [DisplayName] NVARCHAR(128) NOT NULL,
        CONSTRAINT [PK_RepositoryFolderConfig] PRIMARY KEY ([RepositoryId], [LevelOrder])
    );
    PRINT 'DMS: RepositoryFolderConfig table created';
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DocumentWorkflowLink' AND schema_id = SCHEMA_ID('dms'))
BEGIN
    CREATE TABLE [dms].[DocumentWorkflowLink] (
        [DocumentId] UNIQUEIDENTIFIER NOT NULL,
        [RepositoryId] UNIQUEIDENTIFIER NOT NULL,
        [WorkflowInstanceId] UNIQUEIDENTIFIER NOT NULL,
        [WorkflowId] UNIQUEIDENTIFIER NOT NULL,
        [TenantId] UNIQUEIDENTIFIER NOT NULL,
        [LinkedAtUtc] DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT [PK_DocumentWorkflowLink] PRIMARY KEY ([DocumentId], [RepositoryId]),
        INDEX [IX_DocumentWorkflowLink_WorkflowInstance] NONCLUSTERED ([WorkflowInstanceId]),
        INDEX [IX_DocumentWorkflowLink_Tenant_Workflow] NONCLUSTERED ([TenantId], [WorkflowId])
    );
    PRINT 'DMS: DocumentWorkflowLink table created';
END
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
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'sample_items' AND schema_id = SCHEMA_ID('dms'))
BEGIN
    CREATE TABLE [dms].[sample_items] (
        [Id] UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        [TenantId] UNIQUEIDENTIFIER NOT NULL,
        [RepositoryId] UNIQUEIDENTIFIER NOT NULL,
        [Year] SMALLINT NOT NULL,
        [InvoiceType] NVARCHAR(64) NOT NULL,
        [VendorName] NVARCHAR(256) NOT NULL,
        [FileName] NVARCHAR(512) NOT NULL,
        [Status] TINYINT NOT NULL DEFAULT 0,
        [SignStatus] TINYINT NOT NULL DEFAULT 0,
        [CreatedAt] DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        [UpdatedAt] DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        [CreatedBy] UNIQUEIDENTIFIER NOT NULL,
        [UpdatedBy] UNIQUEIDENTIFIER NULL,
        [IsDeleted] BIT NOT NULL DEFAULT 0,
        [DeletedAt] DATETIME2(3) NULL,
        [Version] INT NOT NULL DEFAULT 1,
        [WorkflowInstanceId] UNIQUEIDENTIFIER NULL,
        [ReportNo] NVARCHAR(128) NULL,
        [ReferenceNo] NVARCHAR(64) NULL,
        INDEX [IX_sample_items_Folder] NONCLUSTERED ([RepositoryId], [IsDeleted], [Year], [InvoiceType], [VendorName]) INCLUDE ([Id], [FileName], [Status], [CreatedAt], [WorkflowInstanceId]),
        INDEX [IX_sample_items_Workflow] NONCLUSTERED ([WorkflowInstanceId]) WHERE [WorkflowInstanceId] IS NOT NULL
    );
    PRINT 'DMS: sample_items table created';
END
GO

-- =============================================
-- PART 5: EF MIGRATIONS HISTORY
-- =============================================

PRINT '';
PRINT '=== Setting up EF Migrations History ===';
PRINT '';

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = '__EFMigrationsHistory' AND schema_id = SCHEMA_ID('workflow'))
BEGIN
    CREATE TABLE [workflow].[__EFMigrationsHistory] (
        [MigrationId] NVARCHAR(150) PRIMARY KEY,
        [ProductVersion] NVARCHAR(32) NOT NULL
    );
    PRINT '✓ Workflow EF Migrations History created';
    
    INSERT INTO [workflow].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES ('20260226000001_WorkflowModuleComplete', '8.0.0');
    PRINT '✓ Initial workflow migration entry added';
END
ELSE
BEGIN
    PRINT '✓ Workflow EF Migrations History already exists';
END
GO

-- =============================================
-- VERIFICATION
-- =============================================

PRINT '';
PRINT '=== Tenant Database Setup Complete ===';
PRINT 'Database: ' + DB_NAME();
PRINT '';

PRINT '=== Users Schema Tables ===';
SELECT 
    name AS TableName,
    create_date AS CreatedDate
FROM sys.tables
WHERE schema_id = SCHEMA_ID('users')
ORDER BY name;

PRINT '';
PRINT '=== DMS Schema Tables ===';
SELECT name AS TableName, create_date AS CreatedDate
FROM sys.tables WHERE schema_id = SCHEMA_ID('dms') ORDER BY name;

PRINT '';
PRINT '=== Workflow Schema Tables ===';
SELECT 
    name AS TableName,
    create_date AS CreatedDate,
    CASE 
        WHEN temporal_type = 2 THEN 'Temporal (with history)'
        ELSE 'Regular'
    END AS TableType
FROM sys.tables
WHERE schema_id = SCHEMA_ID('workflow')
ORDER BY name;

DECLARE @WorkflowTableCount INT;
SELECT @WorkflowTableCount = COUNT(*) 
FROM sys.tables 
WHERE schema_id = SCHEMA_ID('workflow')
AND name NOT LIKE '%History';

PRINT '';
PRINT 'Workflow core tables: ' + CAST(@WorkflowTableCount AS NVARCHAR(10)) + ' (Expected: 7)';

IF @WorkflowTableCount = 7
BEGIN
    PRINT '';
    PRINT '✓ SUCCESS! Tenant database is ready!';
    PRINT '';
    PRINT 'Next steps:';
    PRINT '1. Register this tenant in catalog (or use signup API)';
    PRINT '2. Create users in users.Users table';
    PRINT '3. Create workflows in workflow.Workflows table';
    PRINT '4. Publish workflows to create dynamic tables (9 per workflow)';
END
ELSE
BEGIN
    PRINT '';
    PRINT '⚠ WARNING: Expected 7 workflow tables but found ' + CAST(@WorkflowTableCount AS NVARCHAR(10));
    PRINT 'Some tables may be missing. Review the script output above.';
END

PRINT '';
PRINT '=== Setup Complete ===';
GO
