-- =============================================
-- Add [action] to workflow mailbox tables for ALL tenants
--
-- For each tenant:
--   1) Read workflow Id from workflow.Workflows
--   2) Take first 8 chars of workflow Id (GUID without hyphens)
--      Example: a1b2c3d4-.... → Inbox_a1b2c3d4
--   3) ALTER workflow.Inbox_{suffix} / Sent_{suffix} / Completed_{suffix}
--
-- Run against: CATALOG database (ezofis_catalog_*)
-- Safe to re-run (idempotent per tenant / table)
--
-- action: 1 = show verify/approve (default), 0 = hide action buttons
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
DECLARE @TablesUpdated    INT = 0;

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
    SET @TablesUpdated = 0;

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

IF OBJECT_ID(N''workflow.Workflows'', N''U'') IS NULL
BEGIN
    SELECT 0 AS TablesUpdated;
END
ELSE
BEGIN
    DECLARE @WorkflowId UNIQUEIDENTIFIER;
    DECLARE @Suffix NVARCHAR(8);
    DECLARE @TableName SYSNAME;
    DECLARE @ConstraintName SYSNAME;
    DECLARE @AlterSql NVARCHAR(MAX);
    DECLARE @Count INT = 0;

    DECLARE workflow_cursor CURSOR LOCAL FAST_FORWARD FOR
        SELECT [Id]
        FROM [workflow].[Workflows]
        WHERE [Id] IS NOT NULL
          AND ISNULL([IsDeleted], 0) = 0;

    OPEN workflow_cursor;
    FETCH NEXT FROM workflow_cursor INTO @WorkflowId;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        -- Same as C#: workflowId.ToString("N")[..8]
        SET @Suffix = LEFT(REPLACE(LOWER(CONVERT(NVARCHAR(36), @WorkflowId)), N''-'', N''''), 8);

        -- Inbox_{suffix}
        SET @TableName = N''Inbox_'' + @Suffix;
        IF OBJECT_ID(N''workflow.'' + @TableName, N''U'') IS NOT NULL
           AND COL_LENGTH(N''workflow.'' + @TableName, N''action'') IS NULL
        BEGIN
            SET @ConstraintName = N''DF_'' + @TableName + N''_action'';
            SET @AlterSql = N''ALTER TABLE [workflow].['' + @TableName + N''] ADD [action] INT NOT NULL CONSTRAINT ['' + @ConstraintName + N''] DEFAULT (1);'';
            EXEC sp_executesql @AlterSql;
            SET @Count += 1;
        END

        -- Sent_{suffix}
        SET @TableName = N''Sent_'' + @Suffix;
        IF OBJECT_ID(N''workflow.'' + @TableName, N''U'') IS NOT NULL
           AND COL_LENGTH(N''workflow.'' + @TableName, N''action'') IS NULL
        BEGIN
            SET @ConstraintName = N''DF_'' + @TableName + N''_action'';
            SET @AlterSql = N''ALTER TABLE [workflow].['' + @TableName + N''] ADD [action] INT NOT NULL CONSTRAINT ['' + @ConstraintName + N''] DEFAULT (1);'';
            EXEC sp_executesql @AlterSql;
            SET @Count += 1;
        END

        -- Completed_{suffix}
        SET @TableName = N''Completed_'' + @Suffix;
        IF OBJECT_ID(N''workflow.'' + @TableName, N''U'') IS NOT NULL
           AND COL_LENGTH(N''workflow.'' + @TableName, N''action'') IS NULL
        BEGIN
            SET @ConstraintName = N''DF_'' + @TableName + N''_action'';
            SET @AlterSql = N''ALTER TABLE [workflow].['' + @TableName + N''] ADD [action] INT NOT NULL CONSTRAINT ['' + @ConstraintName + N''] DEFAULT (1);'';
            EXEC sp_executesql @AlterSql;
            SET @Count += 1;
        END

        FETCH NEXT FROM workflow_cursor INTO @WorkflowId;
    END

    CLOSE workflow_cursor;
    DEALLOCATE workflow_cursor;

    SELECT @Count AS TablesUpdated;
END';

        IF OBJECT_ID(N'tempdb..#MailboxResult') IS NOT NULL
            DROP TABLE #MailboxResult;

        CREATE TABLE #MailboxResult (TablesUpdated INT);
        INSERT INTO #MailboxResult
        EXEC sp_executesql @Sql;

        SELECT @TablesUpdated = TablesUpdated FROM #MailboxResult;
        DROP TABLE #MailboxResult;

        SET @Applied += 1;
        PRINT CONCAT(
            N'✓ OK    ', @TenantName, N' — [', @DbName,
            N'] from workflow.Workflows → mailbox [action], tables updated: ', ISNULL(@TablesUpdated, 0));
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
PRINT CONCAT(N'Done. Tenants applied: ', @Applied, N', skipped: ', @Skipped, N', failed: ', @Failed);
GO
