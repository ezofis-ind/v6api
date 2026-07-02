using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaaSApp.Users.Infrastructure.Migrations
{
    public partial class AddPermissionCategories : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PermissionCategories",
                schema: "users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermissionCategories", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PermissionCategories_Key",
                schema: "users",
                table: "PermissionCategories",
                column: "Key",
                unique: true);

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

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PermissionCategories",
                schema: "users");
        }
    }
}
