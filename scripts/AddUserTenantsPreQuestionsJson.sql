/*
  Add onboarding pre-questions JSON storage to catalog.UserTenants.
  Run once on the CATALOG database.

  UserTenants.Id is the tenant user id (users.Users.Id) — no separate UserId column.

  Safe to re-run.
*/

IF SCHEMA_ID(N'catalog') IS NULL
    EXEC(N'CREATE SCHEMA [catalog];');

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'UserTenants' AND schema_id = SCHEMA_ID(N'catalog'))
BEGIN
    RAISERROR('Table [catalog].[UserTenants] does not exist. Run 01b_CreateCatalogTables.sql first.', 16, 1);
    RETURN;
END

IF COL_LENGTH('catalog.UserTenants', 'PreQuestionsJson') IS NULL
    ALTER TABLE [catalog].[UserTenants] ADD [PreQuestionsJson] nvarchar(max) NULL;

IF COL_LENGTH('catalog.UserTenants', 'UserId') IS NOT NULL
BEGIN
    IF EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE name = N'IX_UserTenants_UserId_TenantId'
          AND object_id = OBJECT_ID(N'catalog.UserTenants'))
        DROP INDEX [IX_UserTenants_UserId_TenantId] ON [catalog].[UserTenants];

    ALTER TABLE [catalog].[UserTenants] DROP COLUMN [UserId];
END

PRINT 'UserTenants PreQuestionsJson column ensured.';
