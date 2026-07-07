using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace SaaSApp.Users.Infrastructure.Persistence;

/// <summary>Idempotent users schema patches for tenant provisioning and upgrades.</summary>
public static class UsersSchemaEnsurer
{
    internal const string EnsureExtendedUserColumnsSql = """
        IF COL_LENGTH('users.Users', 'PasswordExpiryDays') IS NULL
            ALTER TABLE [users].[Users] ADD [PasswordExpiryDays] int NOT NULL CONSTRAINT [DF_users_Users_PasswordExpiryDays] DEFAULT 90;

        IF COL_LENGTH('users.Users', 'AccountExpiryDate') IS NULL
            ALTER TABLE [users].[Users] ADD [AccountExpiryDate] datetime2 NULL;

        IF COL_LENGTH('users.Users', 'ForcePasswordResetOnLogin') IS NULL
            ALTER TABLE [users].[Users] ADD [ForcePasswordResetOnLogin] bit NOT NULL CONSTRAINT [DF_users_Users_ForcePasswordResetOnLogin] DEFAULT 0;

        IF COL_LENGTH('users.Users', 'EmployeeId') IS NULL
            ALTER TABLE [users].[Users] ADD [EmployeeId] nvarchar(128) NULL;

        IF COL_LENGTH('users.Users', 'BusinessUnit') IS NULL
            ALTER TABLE [users].[Users] ADD [BusinessUnit] nvarchar(128) NULL;

        IF COL_LENGTH('users.Users', 'Location') IS NULL
            ALTER TABLE [users].[Users] ADD [Location] nvarchar(128) NULL;

        IF COL_LENGTH('users.Users', 'GroupName') IS NULL
            ALTER TABLE [users].[Users] ADD [GroupName] nvarchar(128) NULL;

        IF COL_LENGTH('users.Users', 'MfaMethods') IS NULL
            ALTER TABLE [users].[Users] ADD [MfaMethods] nvarchar(64) NULL;
        """;

    public static async Task EnsureExtendedUserColumnsAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(EnsureExtendedUserColumnsSql, connection) { CommandTimeout = 120 };
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public static async Task EnsureExtendedUserColumnsAsync(
        UsersDbContext context,
        CancellationToken cancellationToken = default)
    {
        await context.Database.ExecuteSqlRawAsync(EnsureExtendedUserColumnsSql, cancellationToken);
    }

    internal const string EnsureGroupsTablesSql = """
        IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'Groups' AND schema_id = SCHEMA_ID(N'users'))
        BEGIN
            CREATE TABLE [users].[Groups] (
                [Id] uniqueidentifier NOT NULL,
                [TenantId] uniqueidentifier NOT NULL,
                [Name] nvarchar(128) NOT NULL,
                [Description] nvarchar(512) NULL,
                [CreatedAtUtc] datetime2 NOT NULL,
                [IsDeleted] bit NOT NULL,
                CONSTRAINT [PK_Groups] PRIMARY KEY ([Id])
            );
        END

        IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'UserGroups' AND schema_id = SCHEMA_ID(N'users'))
        BEGIN
            CREATE TABLE [users].[UserGroups] (
                [GroupId] uniqueidentifier NOT NULL,
                [UserId] uniqueidentifier NOT NULL,
                [TenantId] uniqueidentifier NOT NULL,
                CONSTRAINT [PK_UserGroups] PRIMARY KEY ([GroupId], [UserId]),
                CONSTRAINT [FK_UserGroups_Groups_GroupId] FOREIGN KEY ([GroupId])
                    REFERENCES [users].[Groups] ([Id]) ON DELETE CASCADE
            );
        END

        IF NOT EXISTS (
            SELECT 1 FROM sys.indexes
            WHERE name = N'IX_Groups_TenantId_Name'
              AND object_id = OBJECT_ID(N'users.Groups'))
        BEGIN
            CREATE UNIQUE INDEX [IX_Groups_TenantId_Name] ON [users].[Groups] ([TenantId], [Name]);
        END
        """;

    public static async Task EnsureGroupsTablesAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(EnsureGroupsTablesSql, connection) { CommandTimeout = 120 };
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public static async Task EnsureGroupsTablesAsync(
        UsersDbContext context,
        CancellationToken cancellationToken = default)
    {
        await context.Database.ExecuteSqlRawAsync(EnsureGroupsTablesSql, cancellationToken);
    }

    internal const string EnsurePermissionCategoriesSql = """
        IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'PermissionCategories' AND schema_id = SCHEMA_ID(N'users'))
        BEGIN
            CREATE TABLE [users].[PermissionCategories] (
                [Id] uniqueidentifier NOT NULL,
                [Key] nvarchar(64) NOT NULL,
                [Name] nvarchar(128) NOT NULL,
                [SortOrder] int NOT NULL,
                [IsActive] bit NOT NULL CONSTRAINT [DF_users_PermissionCategories_IsActive] DEFAULT 1,
                CONSTRAINT [PK_PermissionCategories] PRIMARY KEY ([Id])
            );
        END

        IF NOT EXISTS (
            SELECT 1 FROM sys.indexes
            WHERE name = N'IX_PermissionCategories_Key'
              AND object_id = OBJECT_ID(N'users.PermissionCategories'))
        BEGIN
            CREATE UNIQUE INDEX [IX_PermissionCategories_Key] ON [users].[PermissionCategories] ([Key]);
        END

        IF NOT EXISTS (SELECT 1 FROM [users].[PermissionCategories] WHERE [Key] = N'dashboard')
            INSERT INTO [users].[PermissionCategories] ([Id], [Key], [Name], [SortOrder], [IsActive])
            VALUES ('a1000001-0000-4000-8000-000000000001', N'dashboard', N'Dashboard', 1, 1);

        IF NOT EXISTS (SELECT 1 FROM [users].[PermissionCategories] WHERE [Key] = N'invoices')
            INSERT INTO [users].[PermissionCategories] ([Id], [Key], [Name], [SortOrder], [IsActive])
            VALUES ('a1000001-0000-4000-8000-000000000002', N'invoices', N'Invoices', 2, 1);

        IF NOT EXISTS (SELECT 1 FROM [users].[PermissionCategories] WHERE [Key] = N'ocr-document-processing')
            INSERT INTO [users].[PermissionCategories] ([Id], [Key], [Name], [SortOrder], [IsActive])
            VALUES ('a1000001-0000-4000-8000-000000000003', N'ocr-document-processing', N'OCR / Document Processing', 3, 1);

        IF NOT EXISTS (SELECT 1 FROM [users].[PermissionCategories] WHERE [Key] = N'workflow-approvals')
            INSERT INTO [users].[PermissionCategories] ([Id], [Key], [Name], [SortOrder], [IsActive])
            VALUES ('a1000001-0000-4000-8000-000000000004', N'workflow-approvals', N'Workflow & Approvals', 4, 1);

        IF NOT EXISTS (SELECT 1 FROM [users].[PermissionCategories] WHERE [Key] = N'reports-analytics')
            INSERT INTO [users].[PermissionCategories] ([Id], [Key], [Name], [SortOrder], [IsActive])
            VALUES ('a1000001-0000-4000-8000-000000000005', N'reports-analytics', N'Reports & Analytics', 5, 1);

        IF NOT EXISTS (SELECT 1 FROM [users].[PermissionCategories] WHERE [Key] = N'user-management')
            INSERT INTO [users].[PermissionCategories] ([Id], [Key], [Name], [SortOrder], [IsActive])
            VALUES ('a1000001-0000-4000-8000-000000000006', N'user-management', N'User Management', 6, 1);

        IF NOT EXISTS (SELECT 1 FROM [users].[PermissionCategories] WHERE [Key] = N'integrations')
            INSERT INTO [users].[PermissionCategories] ([Id], [Key], [Name], [SortOrder], [IsActive])
            VALUES ('a1000001-0000-4000-8000-000000000007', N'integrations', N'Integrations', 7, 1);

        IF NOT EXISTS (SELECT 1 FROM [users].[PermissionCategories] WHERE [Key] = N'system-settings')
            INSERT INTO [users].[PermissionCategories] ([Id], [Key], [Name], [SortOrder], [IsActive])
            VALUES ('a1000001-0000-4000-8000-000000000008', N'system-settings', N'System Settings', 8, 1);
        """;

    public static async Task EnsurePermissionCategoriesAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(EnsurePermissionCategoriesSql, connection) { CommandTimeout = 120 };
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public static async Task EnsurePermissionCategoriesAsync(
        UsersDbContext context,
        CancellationToken cancellationToken = default)
    {
        await context.Database.ExecuteSqlRawAsync(EnsurePermissionCategoriesSql, cancellationToken);
    }

    internal const string EnsureMenusTablesSql = """
        IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'Menus' AND schema_id = SCHEMA_ID(N'users'))
        BEGIN
            CREATE TABLE [users].[Menus] (
                [Id] uniqueidentifier NOT NULL,
                [Key] nvarchar(64) NOT NULL,
                [Label] nvarchar(128) NOT NULL,
                [RoutePath] nvarchar(256) NOT NULL,
                [SortOrder] int NOT NULL,
                [IsSystem] bit NOT NULL,
                [IsDeleted] bit NOT NULL,
                [CreatedAtUtc] datetime2 NOT NULL,
                CONSTRAINT [PK_Menus] PRIMARY KEY ([Id])
            );
        END

        IF NOT EXISTS (
            SELECT 1 FROM sys.indexes
            WHERE name = N'IX_Menus_Key'
              AND object_id = OBJECT_ID(N'users.Menus'))
        BEGIN
            CREATE UNIQUE INDEX [IX_Menus_Key] ON [users].[Menus] ([Key]);
        END

        IF NOT EXISTS (SELECT 1 FROM [users].[Menus] WHERE [Key] = N'dashboard')
            INSERT INTO [users].[Menus] ([Id], [Key], [Label], [RoutePath], [SortOrder], [IsSystem], [IsDeleted], [CreatedAtUtc])
            VALUES ('b2000001-0000-4000-8000-000000000001', N'dashboard', N'Dashboard', N'/dashboard', 1, 1, 0, '2025-07-02T00:00:00');

        IF NOT EXISTS (SELECT 1 FROM [users].[Menus] WHERE [Key] = N'inbox')
            INSERT INTO [users].[Menus] ([Id], [Key], [Label], [RoutePath], [SortOrder], [IsSystem], [IsDeleted], [CreatedAtUtc])
            VALUES ('b2000001-0000-4000-8000-000000000002', N'inbox', N'Inbox', N'/inbox', 2, 1, 0, '2025-07-02T00:00:00');

        IF NOT EXISTS (SELECT 1 FROM [users].[Menus] WHERE [Key] = N'ocr-review')
            INSERT INTO [users].[Menus] ([Id], [Key], [Label], [RoutePath], [SortOrder], [IsSystem], [IsDeleted], [CreatedAtUtc])
            VALUES ('b2000001-0000-4000-8000-000000000003', N'ocr-review', N'OCR.Review', N'/ocr-review', 3, 1, 0, '2025-07-02T00:00:00');

        IF NOT EXISTS (SELECT 1 FROM [users].[Menus] WHERE [Key] = N'processed-invoices')
            INSERT INTO [users].[Menus] ([Id], [Key], [Label], [RoutePath], [SortOrder], [IsSystem], [IsDeleted], [CreatedAtUtc])
            VALUES ('b2000001-0000-4000-8000-000000000004', N'processed-invoices', N'Processed Invoices', N'/processed-invoices', 4, 1, 0, '2025-07-02T00:00:00');

        IF NOT EXISTS (SELECT 1 FROM [users].[Menus] WHERE [Key] = N'approval-queue')
            INSERT INTO [users].[Menus] ([Id], [Key], [Label], [RoutePath], [SortOrder], [IsSystem], [IsDeleted], [CreatedAtUtc])
            VALUES ('b2000001-0000-4000-8000-000000000005', N'approval-queue', N'Approval Queue', N'/approval-queue', 5, 1, 0, '2025-07-02T00:00:00');

        IF NOT EXISTS (SELECT 1 FROM [users].[Menus] WHERE [Key] = N'vendors')
            INSERT INTO [users].[Menus] ([Id], [Key], [Label], [RoutePath], [SortOrder], [IsSystem], [IsDeleted], [CreatedAtUtc])
            VALUES ('b2000001-0000-4000-8000-000000000006', N'vendors', N'Vendors', N'/vendors', 6, 1, 0, '2025-07-02T00:00:00');
        """;

    internal const string EnsureRoleMenusTableSql = """
        IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'RoleMenus' AND schema_id = SCHEMA_ID(N'users'))
        BEGIN
            CREATE TABLE [users].[RoleMenus] (
                [RoleId] uniqueidentifier NOT NULL,
                [MenuId] uniqueidentifier NOT NULL,
                [TenantId] uniqueidentifier NOT NULL,
                [IsDefaultLanding] bit NOT NULL,
                CONSTRAINT [PK_RoleMenus] PRIMARY KEY ([RoleId], [MenuId]),
                CONSTRAINT [FK_RoleMenus_Menus_MenuId] FOREIGN KEY ([MenuId])
                    REFERENCES [users].[Menus] ([Id]) ON DELETE CASCADE,
                CONSTRAINT [FK_RoleMenus_Roles_RoleId] FOREIGN KEY ([RoleId])
                    REFERENCES [users].[Roles] ([Id]) ON DELETE CASCADE
            );
        END

        IF NOT EXISTS (
            SELECT 1 FROM sys.indexes
            WHERE name = N'IX_RoleMenus_MenuId'
              AND object_id = OBJECT_ID(N'users.RoleMenus'))
        BEGIN
            CREATE INDEX [IX_RoleMenus_MenuId] ON [users].[RoleMenus] ([MenuId]);
        END
        """;

    public static async Task EnsureMenusTablesAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(EnsureMenusTablesSql, connection) { CommandTimeout = 120 };
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public static async Task EnsureMenusTablesAsync(
        UsersDbContext context,
        CancellationToken cancellationToken = default)
    {
        await context.Database.ExecuteSqlRawAsync(EnsureMenusTablesSql, cancellationToken);
    }

    internal const string EnsureRoleMenusTablesSql = EnsureMenusTablesSql + EnsureRoleMenusTableSql;

    public static async Task EnsureRoleMenusTablesAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(EnsureRoleMenusTablesSql, connection) { CommandTimeout = 120 };
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public static async Task EnsureRoleMenusTablesAsync(
        UsersDbContext context,
        CancellationToken cancellationToken = default)
    {
        await context.Database.ExecuteSqlRawAsync(EnsureRoleMenusTablesSql, cancellationToken);
    }
}
