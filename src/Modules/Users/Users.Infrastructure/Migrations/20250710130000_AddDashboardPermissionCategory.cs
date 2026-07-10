using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaaSApp.Users.Infrastructure.Migrations
{
    /// <summary>
    /// Adds Dashboard default category and reorders sort for tenants that already applied the 5-category seed.
    /// </summary>
    public partial class AddDashboardPermissionCategory : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM [users].[PermissionCategories] WHERE [Key] = N'dashboard')
                BEGIN
                    INSERT INTO [users].[PermissionCategories] ([Id], [Key], [Name], [SortOrder], [IsActive])
                    VALUES ('a1000001-0000-4000-8000-000000000006', N'dashboard', N'Dashboard', 1, 1);
                END

                UPDATE [users].[PermissionCategories] SET [SortOrder] = 2 WHERE [Key] = N'workflow';
                UPDATE [users].[PermissionCategories] SET [SortOrder] = 3 WHERE [Key] = N'folder';
                UPDATE [users].[PermissionCategories] SET [SortOrder] = 4 WHERE [Key] = N'task';
                UPDATE [users].[PermissionCategories] SET [SortOrder] = 5 WHERE [Key] = N'workspace';
                UPDATE [users].[PermissionCategories] SET [SortOrder] = 6 WHERE [Key] = N'settings';
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE [users].[PermissionCategories] SET [SortOrder] = 1 WHERE [Key] = N'workflow';
                UPDATE [users].[PermissionCategories] SET [SortOrder] = 2 WHERE [Key] = N'folder';
                UPDATE [users].[PermissionCategories] SET [SortOrder] = 3 WHERE [Key] = N'task';
                UPDATE [users].[PermissionCategories] SET [SortOrder] = 4 WHERE [Key] = N'workspace';
                UPDATE [users].[PermissionCategories] SET [SortOrder] = 5 WHERE [Key] = N'settings';

                DELETE FROM [users].[PermissionCategories] WHERE [Key] = N'dashboard' AND [Id] = 'a1000001-0000-4000-8000-000000000006';
                """);
        }
    }
}
