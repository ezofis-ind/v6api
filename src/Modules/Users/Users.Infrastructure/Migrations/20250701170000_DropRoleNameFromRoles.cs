using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaaSApp.Users.Infrastructure.Migrations
{
    public partial class DropRoleNameFromRoles : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE rp
                SET rp.RoleName = r.Name
                FROM users.RolePermissions rp
                INNER JOIN users.Roles r ON r.Id = rp.RoleId
                WHERE rp.RoleName = '' OR rp.RoleName IS NULL;
                """);

            migrationBuilder.DropIndex(
                name: "IX_Roles_TenantId_Name",
                schema: "users",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "Name",
                schema: "users",
                table: "Roles");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Name",
                schema: "users",
                table: "Roles",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE r
                SET r.Name = src.RoleName
                FROM users.Roles r
                INNER JOIN (
                    SELECT RoleId, MIN(RoleName) AS RoleName
                    FROM users.RolePermissions
                    GROUP BY RoleId
                ) src ON src.RoleId = r.Id;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Roles_TenantId_Name",
                schema: "users",
                table: "Roles",
                columns: new[] { "TenantId", "Name" },
                unique: true);
        }
    }
}
