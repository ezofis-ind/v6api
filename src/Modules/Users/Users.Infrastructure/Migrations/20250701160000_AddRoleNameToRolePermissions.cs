using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaaSApp.Users.Infrastructure.Migrations
{
    public partial class AddRoleNameToRolePermissions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RoleName",
                schema: "users",
                table: "RolePermissions",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE rp
                SET rp.RoleName = r.Name
                FROM users.RolePermissions rp
                INNER JOIN users.Roles r ON r.Id = rp.RoleId;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_TenantId_RoleName",
                schema: "users",
                table: "RolePermissions",
                columns: new[] { "TenantId", "RoleName" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RolePermissions_TenantId_RoleName",
                schema: "users",
                table: "RolePermissions");

            migrationBuilder.DropColumn(
                name: "RoleName",
                schema: "users",
                table: "RolePermissions");
        }
    }
}
