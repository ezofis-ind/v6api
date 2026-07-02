using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaaSApp.Users.Infrastructure.Migrations
{
    public partial class RestoreRoleNameOnRoles : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
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
                IF COL_LENGTH('users.RolePermissions', 'RoleName') IS NOT NULL
                BEGIN
                    UPDATE r
                    SET r.Name = src.RoleName
                    FROM users.Roles r
                    INNER JOIN (
                        SELECT RoleId, MIN(RoleName) AS RoleName
                        FROM users.RolePermissions
                        WHERE RoleName <> ''
                        GROUP BY RoleId
                    ) src ON src.RoleId = r.Id
                    WHERE r.Name = '';
                END
                """);

            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = N'IX_RolePermissions_TenantId_RoleName'
                      AND object_id = OBJECT_ID(N'users.RolePermissions'))
                BEGIN
                    DROP INDEX [IX_RolePermissions_TenantId_RoleName] ON [users].[RolePermissions];
                END
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH('users.RolePermissions', 'RoleName') IS NOT NULL
                BEGIN
                    ALTER TABLE [users].[RolePermissions] DROP COLUMN [RoleName];
                END
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Roles_TenantId_Name",
                schema: "users",
                table: "Roles",
                columns: new[] { "TenantId", "Name" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Roles_TenantId_Name",
                schema: "users",
                table: "Roles");

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

            migrationBuilder.DropColumn(
                name: "Name",
                schema: "users",
                table: "Roles");
        }
    }
}
