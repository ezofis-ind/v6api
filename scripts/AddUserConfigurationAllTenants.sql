-- =============================================
-- Add [Configuration] column to [users].[Users]
-- for ALL registered tenants in catalog.Tenants
--
-- Run against: CATALOG database (ezofis_catalog_*)
-- Safe to re-run (idempotent per tenant)
--
-- Configuration: 0 = not completed, 1 = completed
-- Existing rows default to 0 when column is added
-- =============================================

SET NOCOUNT ON;

DECLARE @TenantId       UNIQUEIDENTIFIER;
DECLARE @TenantName     NVARCHAR(256);
DECLARE @ConnectionString NVARCHAR(1024);
DECLARE @DbName         NVARCHAR(256);
DECLARE @Sql            NVARCHAR(MAX);
DECLARE @Applied        INT = 0;
DECLARE @Skipped        INT = 0;
DECLARE @Failed         INT = 0;

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
IF NOT EXISTS (
    SELECT 1
    FROM [' + REPLACE(@DbName, N']', N']]') + N'].sys.tables t
    INNER JOIN [' + REPLACE(@DbName, N']', N']]') + N'].sys.schemas s ON s.schema_id = t.schema_id
    WHERE s.name = N''users'' AND t.name = N''Users'')
BEGIN
    RAISERROR(N''Table [users].[Users] does not exist in this tenant database.'', 16, 1);
END

IF COL_LENGTH(N''' + REPLACE(@DbName, N']', N']]') + N'.users.Users'', N''Configuration'') IS NULL
BEGIN
    ALTER TABLE [' + REPLACE(@DbName, N']', N']]') + N'].[users].[Users]
        ADD [Configuration] INT NOT NULL
            CONSTRAINT [DF_users_Users_Configuration] DEFAULT (0);
END';

        EXEC sp_executesql @Sql;
        SET @Applied += 1;
        PRINT CONCAT(N'✓ OK    ', @TenantName, N' — [', @DbName, N'].[users].[Users].[Configuration]');
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
PRINT CONCAT(N'Done. Applied/skipped-existing: ', @Applied, N', skipped: ', @Skipped, N', failed: ', @Failed);
GO
