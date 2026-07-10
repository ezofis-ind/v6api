using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaaSApp.Users.Infrastructure.Migrations
{
    public partial class ReplacePermissionCategoryDefaults : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "users",
                table: "PermissionCategories",
                keyColumn: "Id",
                keyValue: Guid.Parse("a1000001-0000-4000-8000-000000000001"));

            migrationBuilder.DeleteData(
                schema: "users",
                table: "PermissionCategories",
                keyColumn: "Id",
                keyValue: Guid.Parse("a1000001-0000-4000-8000-000000000002"));

            migrationBuilder.DeleteData(
                schema: "users",
                table: "PermissionCategories",
                keyColumn: "Id",
                keyValue: Guid.Parse("a1000001-0000-4000-8000-000000000003"));

            migrationBuilder.DeleteData(
                schema: "users",
                table: "PermissionCategories",
                keyColumn: "Id",
                keyValue: Guid.Parse("a1000001-0000-4000-8000-000000000004"));

            migrationBuilder.DeleteData(
                schema: "users",
                table: "PermissionCategories",
                keyColumn: "Id",
                keyValue: Guid.Parse("a1000001-0000-4000-8000-000000000005"));

            migrationBuilder.DeleteData(
                schema: "users",
                table: "PermissionCategories",
                keyColumn: "Id",
                keyValue: Guid.Parse("a1000001-0000-4000-8000-000000000006"));

            migrationBuilder.DeleteData(
                schema: "users",
                table: "PermissionCategories",
                keyColumn: "Id",
                keyValue: Guid.Parse("a1000001-0000-4000-8000-000000000007"));

            migrationBuilder.DeleteData(
                schema: "users",
                table: "PermissionCategories",
                keyColumn: "Id",
                keyValue: Guid.Parse("a1000001-0000-4000-8000-000000000008"));

            migrationBuilder.InsertData(
                schema: "users",
                table: "PermissionCategories",
                columns: new[] { "Id", "Key", "Name", "SortOrder", "IsActive" },
                values: new object[,]
                {
                    { Guid.Parse("a1000001-0000-4000-8000-000000000006"), "dashboard", "Dashboard", 1, true },
                    { Guid.Parse("a1000001-0000-4000-8000-000000000001"), "workflow", "Workflow", 2, true },
                    { Guid.Parse("a1000001-0000-4000-8000-000000000002"), "folder", "Folder", 3, true },
                    { Guid.Parse("a1000001-0000-4000-8000-000000000003"), "task", "Task", 4, true },
                    { Guid.Parse("a1000001-0000-4000-8000-000000000004"), "workspace", "Workspace", 5, true },
                    { Guid.Parse("a1000001-0000-4000-8000-000000000005"), "settings", "Settings", 6, true },
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "users",
                table: "PermissionCategories",
                keyColumn: "Id",
                keyValue: Guid.Parse("a1000001-0000-4000-8000-000000000001"));

            migrationBuilder.DeleteData(
                schema: "users",
                table: "PermissionCategories",
                keyColumn: "Id",
                keyValue: Guid.Parse("a1000001-0000-4000-8000-000000000002"));

            migrationBuilder.DeleteData(
                schema: "users",
                table: "PermissionCategories",
                keyColumn: "Id",
                keyValue: Guid.Parse("a1000001-0000-4000-8000-000000000003"));

            migrationBuilder.DeleteData(
                schema: "users",
                table: "PermissionCategories",
                keyColumn: "Id",
                keyValue: Guid.Parse("a1000001-0000-4000-8000-000000000004"));

            migrationBuilder.DeleteData(
                schema: "users",
                table: "PermissionCategories",
                keyColumn: "Id",
                keyValue: Guid.Parse("a1000001-0000-4000-8000-000000000005"));

            migrationBuilder.DeleteData(
                schema: "users",
                table: "PermissionCategories",
                keyColumn: "Id",
                keyValue: Guid.Parse("a1000001-0000-4000-8000-000000000006"));

            migrationBuilder.InsertData(
                schema: "users",
                table: "PermissionCategories",
                columns: new[] { "Id", "Key", "Name", "SortOrder", "IsActive" },
                values: new object[,]
                {
                    { Guid.Parse("a1000001-0000-4000-8000-000000000001"), "dashboard", "Dashboard", 1, true },
                    { Guid.Parse("a1000001-0000-4000-8000-000000000002"), "invoices", "Invoices", 2, true },
                    { Guid.Parse("a1000001-0000-4000-8000-000000000003"), "ocr-document-processing", "OCR / Document Processing", 3, true },
                    { Guid.Parse("a1000001-0000-4000-8000-000000000004"), "workflow-approvals", "Workflow & Approvals", 4, true },
                    { Guid.Parse("a1000001-0000-4000-8000-000000000005"), "reports-analytics", "Reports & Analytics", 5, true },
                    { Guid.Parse("a1000001-0000-4000-8000-000000000006"), "user-management", "User Management", 6, true },
                    { Guid.Parse("a1000001-0000-4000-8000-000000000007"), "integrations", "Integrations", 7, true },
                    { Guid.Parse("a1000001-0000-4000-8000-000000000008"), "system-settings", "System Settings", 8, true },
                });
        }
    }
}
