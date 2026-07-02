using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaaSApp.Users.Infrastructure.Migrations
{
    public partial class AddMenus : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Menus",
                schema: "users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RoutePath = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Menus", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RoleMenus",
                schema: "users",
                columns: table => new
                {
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MenuId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDefaultLanding = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleMenus", x => new { x.RoleId, x.MenuId });
                    table.ForeignKey(
                        name: "FK_RoleMenus_Menus_MenuId",
                        column: x => x.MenuId,
                        principalSchema: "users",
                        principalTable: "Menus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RoleMenus_Roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "users",
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Menus_Key",
                schema: "users",
                table: "Menus",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoleMenus_MenuId",
                schema: "users",
                table: "RoleMenus",
                column: "MenuId");

            migrationBuilder.InsertData(
                schema: "users",
                table: "Menus",
                columns: new[] { "Id", "Key", "Label", "RoutePath", "SortOrder", "IsSystem", "IsDeleted", "CreatedAtUtc" },
                values: new object[,]
                {
                    { new Guid("b2000001-0000-4000-8000-000000000001"), "dashboard", "Dashboard", "/dashboard", 1, true, false, new DateTime(2025, 7, 2, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("b2000001-0000-4000-8000-000000000002"), "inbox", "Inbox", "/inbox", 2, true, false, new DateTime(2025, 7, 2, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("b2000001-0000-4000-8000-000000000003"), "ocr-review", "OCR.Review", "/ocr-review", 3, true, false, new DateTime(2025, 7, 2, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("b2000001-0000-4000-8000-000000000004"), "processed-invoices", "Processed Invoices", "/processed-invoices", 4, true, false, new DateTime(2025, 7, 2, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("b2000001-0000-4000-8000-000000000005"), "approval-queue", "Approval Queue", "/approval-queue", 5, true, false, new DateTime(2025, 7, 2, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("b2000001-0000-4000-8000-000000000006"), "vendors", "Vendors", "/vendors", 6, true, false, new DateTime(2025, 7, 2, 0, 0, 0, 0, DateTimeKind.Utc) }
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RoleMenus",
                schema: "users");

            migrationBuilder.DropTable(
                name: "Menus",
                schema: "users");
        }
    }
}
