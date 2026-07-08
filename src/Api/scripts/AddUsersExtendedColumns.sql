/*
  Add extended user profile / auth columns to [users].[Users].
  Run once per TENANT database (not the catalog DB).

  Safe to re-run: each column is added only if missing.
*/

IF SCHEMA_ID(N'users') IS NULL
    EXEC(N'CREATE SCHEMA [users];');

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'Users' AND schema_id = SCHEMA_ID(N'users'))
BEGIN
    RAISERROR('Table [users].[Users] does not exist. Run tenant provisioning / 02_CreateTenantDatabase.sql first.', 16, 1);
    RETURN;
END

IF COL_LENGTH('users.Users', 'PasswordExpiryDays') IS NULL
    ALTER TABLE [users].[Users] ADD [PasswordExpiryDays] int NOT NULL CONSTRAINT [DF_users_Users_PasswordExpiryDays] DEFAULT 90;

IF COL_LENGTH('users.Users', 'AccountExpiryDate') IS NULL
    ALTER TABLE [users].[Users] ADD [AccountExpiryDate] datetime2 NULL;

IF COL_LENGTH('users.Users', 'ForcePasswordResetOnLogin') IS NULL
    ALTER TABLE [users].[Users] ADD [ForcePasswordResetOnLogin] bit NOT NULL CONSTRAINT [DF_users_Users_ForcePasswordResetOnLogin] DEFAULT 0;

IF COL_LENGTH('users.Users', 'EmployeeId') IS NULL
    ALTER TABLE [users].[Users] ADD [EmployeeId] nvarchar(128) NULL;

IF COL_LENGTH('users.Users', 'BusinessUnit') IS NULL
    ALTER TABLE [users].[Users] ADD [BusinessUnit] nvarchar(128) NULL;

IF COL_LENGTH('users.Users', 'Location') IS NULL
    ALTER TABLE [users].[Users] ADD [Location] nvarchar(128) NULL;

IF COL_LENGTH('users.Users', 'GroupName') IS NULL
    ALTER TABLE [users].[Users] ADD [GroupName] nvarchar(128) NULL;

IF COL_LENGTH('users.Users', 'MfaMethods') IS NULL
    ALTER TABLE [users].[Users] ADD [MfaMethods] nvarchar(64) NULL;

PRINT 'Extended user columns ensured on [users].[Users].';
