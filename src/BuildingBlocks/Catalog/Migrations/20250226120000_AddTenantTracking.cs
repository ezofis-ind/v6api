using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaaSApp.Catalog.Migrations
{
    public partial class AddTenantTracking : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Email",
                schema: "catalog",
                table: "Tenants",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "SignupSource",
                schema: "catalog",
                table: "Tenants",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "Platform",
                schema: "catalog",
                table: "Tenants",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "AppVersion",
                schema: "catalog",
                table: "Tenants",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Email", schema: "catalog", table: "Tenants");
            migrationBuilder.DropColumn(name: "SignupSource", schema: "catalog", table: "Tenants");
            migrationBuilder.DropColumn(name: "Platform", schema: "catalog", table: "Tenants");
            migrationBuilder.DropColumn(name: "AppVersion", schema: "catalog", table: "Tenants");
        }
    }
}
