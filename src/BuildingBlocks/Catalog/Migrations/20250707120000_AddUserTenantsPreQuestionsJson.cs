using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaaSApp.Catalog.Migrations
{
    [Migration("20250707120000_AddUserTenantsPreQuestionsJson")]
    public partial class AddUserTenantsPreQuestionsJson : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('catalog.UserTenants', 'UserId') IS NULL
                    ALTER TABLE [catalog].[UserTenants] ADD [UserId] uniqueidentifier NULL;

                IF COL_LENGTH('catalog.UserTenants', 'PreQuestionsJson') IS NULL
                    ALTER TABLE [catalog].[UserTenants] ADD [PreQuestionsJson] nvarchar(max) NULL;

                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = N'IX_UserTenants_UserId_TenantId'
                      AND object_id = OBJECT_ID(N'catalog.UserTenants'))
                BEGIN
                    CREATE INDEX [IX_UserTenants_UserId_TenantId]
                        ON [catalog].[UserTenants] ([UserId], [TenantId]);
                END
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = N'IX_UserTenants_UserId_TenantId'
                      AND object_id = OBJECT_ID(N'catalog.UserTenants'))
                    DROP INDEX [IX_UserTenants_UserId_TenantId] ON [catalog].[UserTenants];

                IF COL_LENGTH('catalog.UserTenants', 'PreQuestionsJson') IS NOT NULL
                    ALTER TABLE [catalog].[UserTenants] DROP COLUMN [PreQuestionsJson];

                IF COL_LENGTH('catalog.UserTenants', 'UserId') IS NOT NULL
                    ALTER TABLE [catalog].[UserTenants] DROP COLUMN [UserId];
                """);
        }
    }
}
