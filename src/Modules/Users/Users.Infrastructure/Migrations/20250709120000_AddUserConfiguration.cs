using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaaSApp.Users.Infrastructure.Migrations
{
    [Migration("20250709120000_AddUserConfiguration")]
    public partial class AddUserConfiguration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('users.Users', 'Configuration') IS NULL
                    ALTER TABLE [users].[Users] ADD [Configuration] int NOT NULL CONSTRAINT [DF_users_Users_Configuration] DEFAULT 0;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('users.Users', 'Configuration') IS NOT NULL
                    ALTER TABLE [users].[Users] DROP CONSTRAINT [DF_users_Users_Configuration];

                IF COL_LENGTH('users.Users', 'Configuration') IS NOT NULL
                    ALTER TABLE [users].[Users] DROP COLUMN [Configuration];
                """);
        }
    }
}
