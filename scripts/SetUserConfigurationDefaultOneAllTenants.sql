-- =============================================
-- Set [users].[Users].[Configuration] default to 1
-- and update existing rows from 0 -> 1
-- for ALL registered tenants in catalog.Tenants
--
-- Run against: CATALOG database (ezofis_catalog_*)
-- Safe to re-run (idempotent per tenant)
--
-- Configuration: 0 = not completed, 1 = completed
-- =============================================

SET NOCOUNT ON;

DECLARE @TenantId         UNIQUEIDENTIFIER;
DECLARE @TenantName       NVARCHAR(256);
DECLARE @ConnectionString NVARCHAR(1024);
DECLARE @DbName           NVARCHAR(256);
DECLARE @Sql              NVARCHAR(MAX);
DECLARE @Applied          INT = 0;
DECLARE @Skipped          INT = 0;
DECLARE @Failed           INT = 0;
DECLARE @RowsUpdated      INT = 0;

DECLARE tenant_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT [Id], [Name], [ConnectionString]
    FROM [catalog].[Tenants]
    WHERE [IsActive] = 1
      AND [ConnectionString] IS NOT NULL
      AND LTRIM(RTRIM([ConnectionString])) <> N''
    ORDER BY [Name];

OPEN tenant_cursor;
FETCH NEXT FROM tenant_cursor INTO @TenantId, @TenantName, @ConnectionString;

WHILE @@FETCH_STATUS = 0
BEGIN
    SET @DbName = NULL;

    IF @ConnectionString LIKE N'%Initial Catalog=%'
    BEGIN
        SET @DbName = SUBSTRING(
            @ConnectionString,
            CHARINDEX(N'Initial Catalog=', @ConnectionString) + LEN(N'Initial Catalog='),
            1024);
        SET @DbName = LTRIM(RTRIM(LEFT(
            @DbName,
            CASE WHEN CHARINDEX(N';', @DbName) > 0 THEN CHARINDEX(N';', @DbName) - 1 ELSE LEN(@DbName) END)));
    END
    ELSE IF @ConnectionString LIKE N'%Database=%'
    BEGIN
        SET @DbName = SUBSTRING(
            @ConnectionString,
            CHARINDEX(N'Database=', @ConnectionString) + LEN(N'Database='),
            1024);
        SET @DbName = LTRIM(RTRIM(LEFT(
            @DbName,
            CASE WHEN CHARINDEX(N';', @DbName) > 0 THEN CHARINDEX(N';', @DbName) - 1 ELSE LEN(@DbName) END)));
    END

    IF @DbName IS NULL OR @DbName = N''
    BEGIN
        SET @Skipped += 1;
        PRINT CONCAT(N'⊘ SKIP  ', @TenantName, N' (', @TenantId, N') — could not parse database name from ConnectionString');
        GOTO NextTenant;
    END

    IF DB_ID(@DbName) IS NULL
    BEGIN
        SET @Skipped += 1;
        PRINT CONCAT(N'⊘ SKIP  ', @TenantName, N' — database not found: [', @DbName, N']');
        GOTO NextTenant;
    END

    BEGIN TRY
        SET @Sql = N'
USE [' + REPLACE(@DbName, N']', N']]') + N'];

IF COL_LENGTH(N''users.Users'', N''Configuration'') IS NULL
BEGIN
    RAISERROR(N''Column [users].[Users].[Configuration] does not exist. Run AddUserConfigurationAllTenants.sql first.'', 16, 1);
END

DECLARE @Updated INT;
DECLARE @ConstraintName SYSNAME;

UPDATE [users].[Users]
SET [Configuration] = 1
WHERE [Configuration] <> 1;

SET @Updated = @@ROWCOUNT;

SELECT @ConstraintName = dc.[name]
FROM sys.default_constraints dc
INNER JOIN sys.columns c
    ON c.[object_id] = dc.[parent_object_id]
   AND c.[column_id] = dc.[parent_column_id]
WHERE dc.[parent_object_id] = OBJECT_ID(N''[users].[Users]'')
  AND c.[name] = N''Configuration'';

IF @ConstraintName IS NOT NULL
BEGIN
    DECLARE @DropSql NVARCHAR(MAX) = N''ALTER TABLE [users].[Users] DROP CONSTRAINT ['' + REPLACE(@ConstraintName, N'']'', N'']]'') + N''];'';
    EXEC sp_executesql @DropSql;
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c
        ON c.[object_id] = dc.[parent_object_id]
       AND c.[column_id] = dc.[parent_column_id]
    WHERE dc.[parent_object_id] = OBJECT_ID(N''[users].[Users]'')
      AND c.[name] = N''Configuration'')
BEGIN
    ALTER TABLE [users].[Users]
        ADD CONSTRAINT [DF_users_Users_Configuration] DEFAULT (1) FOR [Configuration];
END

SELECT @Updated AS RowsUpdated;';

        IF OBJECT_ID(N'tempdb..#Result') IS NOT NULL
            DROP TABLE #Result;

        CREATE TABLE #Result (RowsUpdated INT);
        INSERT INTO #Result
        EXEC sp_executesql @Sql;

        SET @RowsUpdated = 0;
        SELECT @RowsUpdated = RowsUpdated FROM #Result;
        DROP TABLE #Result;

        SET @Applied += 1;
        PRINT CONCAT(
            N'✓ OK    ', @TenantName, N' — [', @DbName,
            N'].[users].[Users].[Configuration] default=1, rows updated: ', ISNULL(@RowsUpdated, 0));
    END TRY
    BEGIN CATCH
        SET @Failed += 1;
        PRINT CONCAT(
            N'✗ FAIL  ', @TenantName, N' — [', @DbName, N'] — ',
            ERROR_MESSAGE());
    END CATCH

NextTenant:
    FETCH NEXT FROM tenant_cursor INTO @TenantId, @TenantName, @ConnectionString;
END

CLOSE tenant_cursor;
DEALLOCATE tenant_cursor;

PRINT N'';
PRINT CONCAT(N'Done. Applied: ', @Applied, N', skipped: ', @Skipped, N', failed: ', @Failed);
GO
