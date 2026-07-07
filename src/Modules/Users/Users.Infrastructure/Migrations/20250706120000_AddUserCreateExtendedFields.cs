using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaaSApp.Users.Infrastructure.Migrations
{
    [Migration("20250706120000_AddUserCreateExtendedFields")]
    public partial class AddUserCreateExtendedFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SaaSApp.Users.Infrastructure.Persistence.UsersSchemaEnsurer.EnsureExtendedUserColumnsSql);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('users.Users', 'MfaMethods') IS NOT NULL
                    ALTER TABLE [users].[Users] DROP COLUMN [MfaMethods];

                IF COL_LENGTH('users.Users', 'GroupName') IS NOT NULL
                    ALTER TABLE [users].[Users] DROP COLUMN [GroupName];

                IF COL_LENGTH('users.Users', 'Location') IS NOT NULL
                    ALTER TABLE [users].[Users] DROP COLUMN [Location];

                IF COL_LENGTH('users.Users', 'BusinessUnit') IS NOT NULL
                    ALTER TABLE [users].[Users] DROP COLUMN [BusinessUnit];

                IF COL_LENGTH('users.Users', 'EmployeeId') IS NOT NULL
                    ALTER TABLE [users].[Users] DROP COLUMN [EmployeeId];

                IF COL_LENGTH('users.Users', 'ForcePasswordResetOnLogin') IS NOT NULL
                    ALTER TABLE [users].[Users] DROP CONSTRAINT [DF_users_Users_ForcePasswordResetOnLogin];

                IF COL_LENGTH('users.Users', 'ForcePasswordResetOnLogin') IS NOT NULL
                    ALTER TABLE [users].[Users] DROP COLUMN [ForcePasswordResetOnLogin];

                IF COL_LENGTH('users.Users', 'AccountExpiryDate') IS NOT NULL
                    ALTER TABLE [users].[Users] DROP COLUMN [AccountExpiryDate];

                IF COL_LENGTH('users.Users', 'PasswordExpiryDays') IS NOT NULL
                    ALTER TABLE [users].[Users] DROP CONSTRAINT [DF_users_Users_PasswordExpiryDays];

                IF COL_LENGTH('users.Users', 'PasswordExpiryDays') IS NOT NULL
                    ALTER TABLE [users].[Users] DROP COLUMN [PasswordExpiryDays];
                """);
        }
    }
}
