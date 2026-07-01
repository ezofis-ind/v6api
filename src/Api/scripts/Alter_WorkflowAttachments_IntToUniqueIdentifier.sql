/*
  Migrate workflow.WorkflowAttachments_{suffix}
    RepositoryId  INT  -> UNIQUEIDENTIFIER
    ItemId        INT  -> UNIQUEIDENTIFIER

  Uses add/backfill/drop/rename so FormJsonId can fill ItemId before INT column is removed.

  Suffix = LEFT(workflowId without dashes, 8)
  Example: workflow a1b2c3d4-e5f6-... -> table WorkflowAttachments_a1b2c3d4

  Run on TENANT database.
*/

SET NOCOUNT ON;

DECLARE @WorkflowId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000000'; -- <<< SET THIS

DECLARE @Suffix    NVARCHAR(8) = LEFT(REPLACE(CAST(@WorkflowId AS NVARCHAR(36)), '-', ''), 8);
DECLARE @Table     SYSNAME     = N'WorkflowAttachments_' + @Suffix;
DECLARE @Qualified NVARCHAR(300) = N'workflow.[' + @Table + N']';
DECLARE @Sql       NVARCHAR(MAX);

IF NOT EXISTS (
    SELECT 1 FROM sys.tables t
    JOIN sys.schemas s ON s.schema_id = t.schema_id
    WHERE s.name = N'workflow' AND t.name = @Table)
BEGIN
    RAISERROR('Table %s not found. Check @WorkflowId.', 16, 1, @Qualified);
    RETURN;
END

PRINT 'Table: ' + @Qualified;

/* ------------------------------------------------------------------ */
/* RepositoryId: INT -> UNIQUEIDENTIFIER                              */
/* ------------------------------------------------------------------ */
IF EXISTS (
    SELECT 1 FROM sys.columns c
    JOIN sys.types ty ON ty.user_type_id = c.user_type_id
    JOIN sys.tables t ON t.object_id = c.object_id
    JOIN sys.schemas s ON s.schema_id = t.schema_id
    WHERE s.name = N'workflow' AND t.name = @Table
      AND c.name = N'RepositoryId' AND ty.name IN (N'int', N'bigint', N'smallint'))
BEGIN
    IF COL_LENGTH('workflow.' + @Table, 'RepositoryId_New') IS NULL
    BEGIN
        SET @Sql = N'ALTER TABLE ' + @Qualified + N' ADD RepositoryId_New UNIQUEIDENTIFIER NULL;';
        EXEC sp_executesql @Sql;
    END

    -- Backfill from workflow.Workflows.RepositoryId when it is a GUID string
    SET @Sql = N'
    UPDATE a
    SET a.RepositoryId_New = TRY_CONVERT(UNIQUEIDENTIFIER, NULLIF(LTRIM(RTRIM(wf.RepositoryId)), N''''))
    FROM ' + @Qualified + N' a
    CROSS JOIN workflow.Workflows wf
    WHERE wf.Id = @WorkflowId AND wf.IsDeleted = 0
      AND a.RepositoryId_New IS NULL
      AND TRY_CONVERT(UNIQUEIDENTIFIER, NULLIF(LTRIM(RTRIM(wf.RepositoryId)), N'''')) IS NOT NULL;';
    EXEC sp_executesql @Sql, N'@WorkflowId UNIQUEIDENTIFIER', @WorkflowId = @WorkflowId;

    SET @Sql = N'ALTER TABLE ' + @Qualified + N' DROP COLUMN RepositoryId;';
    EXEC sp_executesql @Sql;

    EXEC sys.sp_rename
        @objname = N'workflow.' + @Table + N'.RepositoryId_New',
        @newname = N'RepositoryId',
        @objtype = N'COLUMN';

    PRINT 'RepositoryId: INT -> UNIQUEIDENTIFIER (done).';
END
ELSE IF COL_LENGTH('workflow.' + @Table, 'RepositoryId') IS NULL
BEGIN
    SET @Sql = N'ALTER TABLE ' + @Qualified + N' ADD RepositoryId UNIQUEIDENTIFIER NULL;';
    EXEC sp_executesql @Sql;
    PRINT 'RepositoryId: added as UNIQUEIDENTIFIER.';
END
ELSE
    PRINT 'RepositoryId: already UNIQUEIDENTIFIER — skipped.';

/* ------------------------------------------------------------------ */
/* ItemId: INT -> UNIQUEIDENTIFIER                                    */
/* ------------------------------------------------------------------ */
IF EXISTS (
    SELECT 1 FROM sys.columns c
    JOIN sys.types ty ON ty.user_type_id = c.user_type_id
    JOIN sys.tables t ON t.object_id = c.object_id
    JOIN sys.schemas s ON s.schema_id = t.schema_id
    WHERE s.name = N'workflow' AND t.name = @Table
      AND c.name = N'ItemId' AND ty.name IN (N'int', N'bigint', N'smallint'))
BEGIN
    IF COL_LENGTH('workflow.' + @Table, 'ItemId_New') IS NULL
    BEGIN
        SET @Sql = N'ALTER TABLE ' + @Qualified + N' ADD ItemId_New UNIQUEIDENTIFIER NULL;';
        EXEC sp_executesql @Sql;
    END

    -- FormJsonId with dashes
    SET @Sql = N'
    UPDATE ' + @Qualified + N'
    SET ItemId_New = TRY_CONVERT(UNIQUEIDENTIFIER, FormJsonId)
    WHERE ItemId_New IS NULL
      AND FormJsonId IS NOT NULL
      AND TRY_CONVERT(UNIQUEIDENTIFIER, FormJsonId) IS NOT NULL;';
    EXEC sp_executesql @Sql;

    -- FormJsonId 32-char N format (no dashes)
    SET @Sql = N'
    UPDATE a
    SET ItemId_New = CONVERT(UNIQUEIDENTIFIER,
            STUFF(STUFF(STUFF(STUFF(LTRIM(RTRIM(a.FormJsonId)), 9, 0, ''-''), 14, 0, ''-''), 19, 0, ''-''), 24, 0, ''-''))
    FROM ' + @Qualified + N' a
    WHERE a.ItemId_New IS NULL
      AND a.FormJsonId IS NOT NULL
      AND LEN(LTRIM(RTRIM(a.FormJsonId))) = 32
      AND LTRIM(RTRIM(a.FormJsonId)) NOT LIKE ''%[^0-9A-Fa-f]%'';';
    EXEC sp_executesql @Sql;

    SET @Sql = N'ALTER TABLE ' + @Qualified + N' DROP COLUMN ItemId;';
    EXEC sp_executesql @Sql;

    EXEC sys.sp_rename
        @objname = N'workflow.' + @Table + N'.ItemId_New',
        @newname = N'ItemId',
        @objtype = N'COLUMN';

    PRINT 'ItemId: INT -> UNIQUEIDENTIFIER (done).';
END
ELSE IF COL_LENGTH('workflow.' + @Table, 'ItemId') IS NULL
BEGIN
    SET @Sql = N'ALTER TABLE ' + @Qualified + N' ADD ItemId UNIQUEIDENTIFIER NULL;';
    EXEC sp_executesql @Sql;
    PRINT 'ItemId: added as UNIQUEIDENTIFIER.';
END
ELSE
    PRINT 'ItemId: already UNIQUEIDENTIFIER — skipped.';

/* Optional: remove mistaken columns */
IF COL_LENGTH('workflow.' + @Table, 'RepositoryGuid') IS NOT NULL
BEGIN
    SET @Sql = N'ALTER TABLE ' + @Qualified + N' DROP COLUMN RepositoryGuid;';
    EXEC sp_executesql @Sql;
    PRINT 'Dropped RepositoryGuid.';
END
IF COL_LENGTH('workflow.' + @Table, 'ItemGuid') IS NOT NULL
BEGIN
    SET @Sql = N'ALTER TABLE ' + @Qualified + N' DROP COLUMN ItemGuid;';
    EXEC sp_executesql @Sql;
    PRINT 'Dropped ItemGuid.';
END

PRINT 'Complete.';
GO
