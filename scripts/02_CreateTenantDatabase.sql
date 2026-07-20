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

-- Custom roles tables
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Roles' AND schema_id = SCHEMA_ID('users'))
BEGIN
    CREATE TABLE [users].[Roles] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        [TenantId] UNIQUEIDENTIFIER NOT NULL,
        [Name] NVARCHAR(128) NOT NULL,
        [Description] NVARCHAR(512) NULL,
        [CreatedAtUtc] DATETIME2 NOT NULL,
        [IsDeleted] BIT NOT NULL DEFAULT 0,
        CONSTRAINT [IX_Roles_TenantId_Name] UNIQUE ([TenantId], [Name])
    );
    PRINT '✓ Roles table created';
END
ELSE
BEGIN
    PRINT '✓ Roles table already exists';
    IF COL_LENGTH('users.Roles', 'Name') IS NULL
    BEGIN
        ALTER TABLE [users].[Roles] ADD [Name] NVARCHAR(128) NOT NULL CONSTRAINT [DF_Roles_Name] DEFAULT '';
        IF COL_LENGTH('users.RolePermissions', 'RoleName') IS NOT NULL
        BEGIN
            UPDATE r SET r.Name = src.RoleName
            FROM [users].[Roles] r
            INNER JOIN (
                SELECT RoleId, MIN(RoleName) AS RoleName
                FROM [users].[RolePermissions]
                WHERE RoleName <> ''
                GROUP BY RoleId
            ) src ON src.RoleId = r.Id
            WHERE r.Name = '';
        END
        ALTER TABLE [users].[Roles] DROP CONSTRAINT [DF_Roles_Name];
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Roles_TenantId_Name' AND object_id = OBJECT_ID('users.Roles'))
            ALTER TABLE [users].[Roles] ADD CONSTRAINT [IX_Roles_TenantId_Name] UNIQUE ([TenantId], [Name]);
        PRINT '✓ Roles.Name column added';
    END
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'UserRoles' AND schema_id = SCHEMA_ID('users'))
BEGIN
    CREATE TABLE [users].[UserRoles] (
        [RoleId] UNIQUEIDENTIFIER NOT NULL,
        [UserId] UNIQUEIDENTIFIER NOT NULL,
        [TenantId] UNIQUEIDENTIFIER NOT NULL,
        CONSTRAINT [PK_UserRoles] PRIMARY KEY ([RoleId], [UserId]),
        CONSTRAINT [FK_UserRoles_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [users].[Roles]([Id]) ON DELETE CASCADE
    );
    PRINT '✓ UserRoles table created';
END
ELSE
BEGIN
    PRINT '✓ UserRoles table already exists';
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'RolePermissions' AND schema_id = SCHEMA_ID('users'))
BEGIN
    CREATE TABLE [users].[RolePermissions] (
        [RoleId] UNIQUEIDENTIFIER NOT NULL,
        [PermissionKey] NVARCHAR(128) NOT NULL,
        [TenantId] UNIQUEIDENTIFIER NOT NULL,
        CONSTRAINT [PK_RolePermissions] PRIMARY KEY ([RoleId], [PermissionKey]),
        CONSTRAINT [FK_RolePermissions_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [users].[Roles]([Id]) ON DELETE CASCADE
    );
    PRINT '✓ RolePermissions table created';
END
ELSE
BEGIN
    PRINT '✓ RolePermissions table already exists';
    IF COL_LENGTH('users.RolePermissions', 'RoleName') IS NOT NULL
    BEGIN
        IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RolePermissions_TenantId_RoleName' AND object_id = OBJECT_ID('users.RolePermissions'))
            DROP INDEX [IX_RolePermissions_TenantId_RoleName] ON [users].[RolePermissions];
        ALTER TABLE [users].[RolePermissions] DROP COLUMN [RoleName];
        PRINT '✓ RolePermissions.RoleName column removed';
    END
END
GO

-- User groups tables
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Groups' AND schema_id = SCHEMA_ID('users'))
BEGIN
    CREATE TABLE [users].[Groups] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        [TenantId] UNIQUEIDENTIFIER NOT NULL,
        [Name] NVARCHAR(128) NOT NULL,
        [Description] NVARCHAR(512) NULL,
        [CreatedAtUtc] DATETIME2 NOT NULL,
        [IsDeleted] BIT NOT NULL DEFAULT 0,
        CONSTRAINT [IX_Groups_TenantId_Name] UNIQUE ([TenantId], [Name])
    );
    PRINT '✓ Groups table created';
END
ELSE
BEGIN
    PRINT '✓ Groups table already exists';
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'UserGroups' AND schema_id = SCHEMA_ID('users'))
BEGIN
    CREATE TABLE [users].[UserGroups] (
        [GroupId] UNIQUEIDENTIFIER NOT NULL,
        [UserId] UNIQUEIDENTIFIER NOT NULL,
        [TenantId] UNIQUEIDENTIFIER NOT NULL,
        CONSTRAINT [PK_UserGroups] PRIMARY KEY ([GroupId], [UserId]),
        CONSTRAINT [FK_UserGroups_Groups_GroupId] FOREIGN KEY ([GroupId]) REFERENCES [users].[Groups]([Id]) ON DELETE CASCADE
    );
    PRINT '✓ UserGroups table created';
END
ELSE
BEGIN
    PRINT '✓ UserGroups table already exists';
END
GO

-- Navigation menus
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Menus' AND schema_id = SCHEMA_ID('users'))
BEGIN
    CREATE TABLE [users].[Menus] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        [Key] NVARCHAR(64) NOT NULL,
        [Label] NVARCHAR(128) NOT NULL,
        [RoutePath] NVARCHAR(256) NOT NULL,
        [SortOrder] INT NOT NULL,
        [IsSystem] BIT NOT NULL DEFAULT 0,
        [IsDeleted] BIT NOT NULL DEFAULT 0,
        [CreatedAtUtc] DATETIME2 NOT NULL,
        CONSTRAINT [IX_Menus_Key] UNIQUE ([Key])
    );
    PRINT '✓ Menus table created';

    INSERT INTO [users].[Menus] ([Id], [Key], [Label], [RoutePath], [SortOrder], [IsSystem], [IsDeleted], [CreatedAtUtc]) VALUES
        ('b2000001-0000-4000-8000-000000000001', 'dashboard', 'Dashboard', '/dashboard', 1, 1, 0, '2025-07-02T00:00:00'),
        ('b2000001-0000-4000-8000-000000000002', 'inbox', 'Inbox', '/inbox', 2, 1, 0, '2025-07-02T00:00:00'),
        ('b2000001-0000-4000-8000-000000000003', 'ocr-review', 'OCR.Review', '/ocr-review', 3, 1, 0, '2025-07-02T00:00:00'),
        ('b2000001-0000-4000-8000-000000000004', 'processed-invoices', 'Processed Invoices', '/processed-invoices', 4, 1, 0, '2025-07-02T00:00:00'),
        ('b2000001-0000-4000-8000-000000000005', 'approval-queue', 'Approval Queue', '/approval-queue', 5, 1, 0, '2025-07-02T00:00:00'),
        ('b2000001-0000-4000-8000-000000000006', 'vendors', 'Vendors', '/vendors', 6, 1, 0, '2025-07-02T00:00:00');
    PRINT '✓ Menus seed data inserted';
END
ELSE
BEGIN
    PRINT '✓ Menus table already exists';
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'RoleMenus' AND schema_id = SCHEMA_ID('users'))
BEGIN
    CREATE TABLE [users].[RoleMenus] (
        [RoleId] UNIQUEIDENTIFIER NOT NULL,
        [MenuId] UNIQUEIDENTIFIER NOT NULL,
        [TenantId] UNIQUEIDENTIFIER NOT NULL,
        [IsDefaultLanding] BIT NOT NULL DEFAULT 0,
        CONSTRAINT [PK_RoleMenus] PRIMARY KEY ([RoleId], [MenuId]),
        CONSTRAINT [FK_RoleMenus_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [users].[Roles]([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_RoleMenus_Menus_MenuId] FOREIGN KEY ([MenuId]) REFERENCES [users].[Menus]([Id]) ON DELETE CASCADE
    );
    PRINT '✓ RoleMenus table created';
END
ELSE
BEGIN
    PRINT '✓ RoleMenus table already exists';
END
GO

-- Permission categories (system-defined matrix rows)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PermissionCategories' AND schema_id = SCHEMA_ID('users'))
BEGIN
    CREATE TABLE [users].[PermissionCategories] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        [Key] NVARCHAR(64) NOT NULL,
        [Name] NVARCHAR(128) NOT NULL,
        [SortOrder] INT NOT NULL,
        [IsActive] BIT NOT NULL DEFAULT 1,
        CONSTRAINT [IX_PermissionCategories_Key] UNIQUE ([Key])
    );
    PRINT '✓ PermissionCategories table created';

    INSERT INTO [users].[PermissionCategories] ([Id], [Key], [Name], [SortOrder], [IsActive]) VALUES
        ('a1000001-0000-4000-8000-000000000001', 'dashboard', 'Dashboard', 1, 1),
        ('a1000001-0000-4000-8000-000000000002', 'invoices', 'Invoices', 2, 1),
        ('a1000001-0000-4000-8000-000000000003', 'ocr-document-processing', 'OCR / Document Processing', 3, 1),
        ('a1000001-0000-4000-8000-000000000004', 'workflow-approvals', 'Workflow & Approvals', 4, 1),
        ('a1000001-0000-4000-8000-000000000005', 'reports-analytics', 'Reports & Analytics', 5, 1),
        ('a1000001-0000-4000-8000-000000000006', 'user-management', 'User Management', 6, 1),
        ('a1000001-0000-4000-8000-000000000007', 'integrations', 'Integrations', 7, 1),
        ('a1000001-0000-4000-8000-000000000008', 'system-settings', 'System Settings', 8, 1);
    PRINT '✓ PermissionCategories seed data inserted';
END
ELSE
BEGIN
    PRINT '✓ PermissionCategories table already exists';
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

IF EXISTS (SELECT * FROM sys.tables WHERE name = '__EFMigrationsHistory' AND schema_id = SCHEMA_ID('users'))
   AND NOT EXISTS (SELECT 1 FROM [users].[__EFMigrationsHistory] WHERE [MigrationId] = '20250701140000_AddCustomRoles')
BEGIN
    INSERT INTO [users].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES ('20250701140000_AddCustomRoles', '8.0.11');
    PRINT '✓ AddCustomRoles migration entry added';
END
GO

IF EXISTS (SELECT * FROM sys.tables WHERE name = '__EFMigrationsHistory' AND schema_id = SCHEMA_ID('users'))
   AND NOT EXISTS (SELECT 1 FROM [users].[__EFMigrationsHistory] WHERE [MigrationId] = '20250701150000_AddPermissionCategories')
BEGIN
    INSERT INTO [users].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES ('20250701150000_AddPermissionCategories', '8.0.11');
    PRINT '✓ AddPermissionCategories migration entry added';
END
GO

IF EXISTS (SELECT * FROM sys.tables WHERE name = '__EFMigrationsHistory' AND schema_id = SCHEMA_ID('users'))
   AND NOT EXISTS (SELECT 1 FROM [users].[__EFMigrationsHistory] WHERE [MigrationId] = '20250702110000_AddMenus')
BEGIN
    INSERT INTO [users].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES ('20250702110000_AddMenus', '8.0.11');
    PRINT '✓ AddMenus migration entry added';
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
-- PART 5: CONNECTOR (modern OAuth — tenant DB)
-- =============================================

PRINT '';
PRINT '=== Creating dbo.connector (modern OAuth schema) ===';
PRINT '';

IF OBJECT_ID(N'[dbo].[connector]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[connector] (
        [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_connector] PRIMARY KEY,
        [Name] NVARCHAR(256) NOT NULL,
        [ProviderCode] NVARCHAR(64) NOT NULL,
        [ConfigJson] NVARCHAR(MAX) NULL,
        [AccessToken] NVARCHAR(MAX) NULL,
        [RefreshToken] NVARCHAR(MAX) NULL,
        [TokenExpiresAtUtc] DATETIME2(3) NULL,
        [ExternalAccountEmail] NVARCHAR(320) NULL,
        [ExternalAccountId] NVARCHAR(256) NULL,
        [OAuthStatus] NVARCHAR(32) NOT NULL CONSTRAINT [DF_connector_OAuthStatus] DEFAULT (N'Pending'),
        [IsDefault] BIT NOT NULL CONSTRAINT [DF_connector_IsDefault] DEFAULT (0),
        [CreatedAtUtc] DATETIME2(3) NOT NULL CONSTRAINT [DF_connector_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
        [ModifiedAtUtc] DATETIME2(3) NULL,
        [CreatedBy] UNIQUEIDENTIFIER NOT NULL,
        [ModifiedBy] UNIQUEIDENTIFIER NULL,
        [IsDeleted] BIT NOT NULL CONSTRAINT [DF_connector_IsDeleted] DEFAULT (0)
    );
    CREATE INDEX [IX_connector_IsDeleted] ON [dbo].[connector] ([IsDeleted]);
    CREATE INDEX [IX_connector_ProviderCode] ON [dbo].[connector] ([ProviderCode]) WHERE [IsDeleted] = 0;
    PRINT '✓ dbo.connector created (modern schema)';
END
ELSE IF COL_LENGTH('dbo.connector', 'ProviderCode') IS NULL
BEGIN
    PRINT '⚠ Legacy dbo.connector detected. Run scripts/Create-Connector-Table.sql to migrate to modern schema.';
END
ELSE
BEGIN
    PRINT '✓ dbo.connector already exists (modern schema)';
END
GO

-- =============================================
-- PART 6: EF MIGRATIONS HISTORY
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

PRINT '';
PRINT '=== dbo.connector ===';
IF OBJECT_ID(N'dbo.connector', N'U') IS NOT NULL
    PRINT '✓ dbo.connector present (OAuth columns: accessToken, refreshToken, oauthStatus, ...)';
ELSE
    PRINT '⚠ dbo.connector missing';

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
    PRINT '5. Connect OAuth providers via POST /api/connector/oauth/authorize';
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
