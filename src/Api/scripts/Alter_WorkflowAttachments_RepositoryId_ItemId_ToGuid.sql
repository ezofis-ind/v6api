/*
  ALTER workflow.WorkflowAttachments_{suffix}
  - RepositoryId  -> UNIQUEIDENTIFIER
  - ItemId        -> UNIQUEIDENTIFIER
  - Drops RepositoryGuid / ItemGuid if present (not used by API)

  Suffix = first 8 chars of workflow Id without dashes.
  Run on TENANT database.
*/

SET NOCOUNT ON;

-- ========== SET YOUR WORKFLOW ID ==========
DECLARE @WorkflowId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000000'; -- replace
-- ==========================================

DECLARE @Suffix     NVARCHAR(8)  = LEFT(REPLACE(CAST(@WorkflowId AS NVARCHAR(36)), '-', ''), 8);
DECLARE @TableName  SYSNAME      = N'WorkflowAttachments_' + @Suffix;
DECLARE @Sql        NVARCHAR(MAX);

IF NOT EXISTS (
    SELECT 1 FROM sys.tables t
    INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
    WHERE s.name = N'workflow' AND t.name = @TableName)
BEGIN
    RAISERROR('Table workflow.%s does not exist. Create it first or check @WorkflowId.', 16, 1, @TableName);
    RETURN;
END

PRINT 'Altering workflow.[' + @TableName + '] ...';

-- Drop mistaken columns (from earlier bad migration)
IF COL_LENGTH('workflow.' + @TableName, 'RepositoryGuid') IS NOT NULL
BEGIN
    SET @Sql = N'ALTER TABLE workflow.[' + @TableName + N'] DROP COLUMN RepositoryGuid;';
    EXEC sp_executesql @Sql;
    PRINT 'Dropped RepositoryGuid.';
END

IF COL_LENGTH('workflow.' + @TableName, 'ItemGuid') IS NOT NULL
BEGIN
    SET @Sql = N'ALTER TABLE workflow.[' + @TableName + N'] DROP COLUMN ItemGuid;';
    EXEC sp_executesql @Sql;
    PRINT 'Dropped ItemGuid.';
END

-- RepositoryId
IF COL_LENGTH('workflow.' + @TableName, 'RepositoryId') IS NULL
BEGIN
    SET @Sql = N'ALTER TABLE workflow.[' + @TableName + N'] ADD RepositoryId UNIQUEIDENTIFIER NULL;';
    EXEC sp_executesql @Sql;
    PRINT 'Added RepositoryId UNIQUEIDENTIFIER.';
END
ELSE IF EXISTS (
    SELECT 1 FROM sys.columns c
    INNER JOIN sys.types ty ON ty.user_type_id = c.user_type_id
    INNER JOIN sys.tables t ON t.object_id = c.object_id
    INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
    WHERE s.name = N'workflow' AND t.name = @TableName
      AND c.name = N'RepositoryId' AND ty.name IN (N'int', N'bigint', N'smallint'))
BEGIN
    SET @Sql = N'
    ALTER TABLE workflow.[' + @TableName + N'] DROP COLUMN RepositoryId;
    ALTER TABLE workflow.[' + @TableName + N'] ADD RepositoryId UNIQUEIDENTIFIER NULL;';
    EXEC sp_executesql @Sql;
    PRINT 'RepositoryId: INT -> UNIQUEIDENTIFIER (old INT values removed).';
END
ELSE
    PRINT 'RepositoryId already UNIQUEIDENTIFIER (or compatible).';

-- ItemId
IF COL_LENGTH('workflow.' + @TableName, 'ItemId') IS NULL
BEGIN
    SET @Sql = N'ALTER TABLE workflow.[' + @TableName + N'] ADD ItemId UNIQUEIDENTIFIER NULL;';
    EXEC sp_executesql @Sql;
    PRINT 'Added ItemId UNIQUEIDENTIFIER.';
END
ELSE IF EXISTS (
    SELECT 1 FROM sys.columns c
    INNER JOIN sys.types ty ON ty.user_type_id = c.user_type_id
    INNER JOIN sys.tables t ON t.object_id = c.object_id
    INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
    WHERE s.name = N'workflow' AND t.name = @TableName
      AND c.name = N'ItemId' AND ty.name IN (N'int', N'bigint', N'smallint'))
BEGIN
    SET @Sql = N'
    ALTER TABLE workflow.[' + @TableName + N'] DROP COLUMN ItemId;
    ALTER TABLE workflow.[' + @TableName + N'] ADD ItemId UNIQUEIDENTIFIER NULL;';
    EXEC sp_executesql @Sql;
    PRINT 'ItemId: INT -> UNIQUEIDENTIFIER (old INT values removed).';
END
ELSE
    PRINT 'ItemId already UNIQUEIDENTIFIER (or compatible).';

-- Backfill ItemId from FormJsonId (GUID with dashes)
SET @Sql = N'
UPDATE workflow.[' + @TableName + N']
SET ItemId = TRY_CONVERT(UNIQUEIDENTIFIER, FormJsonId)
WHERE ItemId IS NULL
  AND FormJsonId IS NOT NULL
  AND TRY_CONVERT(UNIQUEIDENTIFIER, FormJsonId) IS NOT NULL;';
EXEC sp_executesql @Sql;
PRINT 'Backfilled ItemId from FormJsonId (dashed GUID format).';

-- Backfill ItemId from FormJsonId 32-char N format (no dashes)
SET @Sql = N'
UPDATE a
SET ItemId = CONVERT(UNIQUEIDENTIFIER,
        STUFF(STUFF(STUFF(STUFF(LTRIM(RTRIM(a.FormJsonId)), 9, 0, ''-''), 14, 0, ''-''), 19, 0, ''-''), 24, 0, ''-''))
FROM workflow.[' + @TableName + N'] a
WHERE a.ItemId IS NULL
  AND a.FormJsonId IS NOT NULL
  AND LEN(LTRIM(RTRIM(a.FormJsonId))) = 32
  AND LTRIM(RTRIM(a.FormJsonId)) NOT LIKE ''%[^0-9A-Fa-f]%'';';
EXEC sp_executesql @Sql;
PRINT 'Backfilled ItemId from FormJsonId (32-char N format).';

-- Backfill RepositoryId from workflow.Workflows.RepositoryId (GUID link)
SET @Sql = N'
UPDATE a
SET a.RepositoryId = TRY_CONVERT(UNIQUEIDENTIFIER, NULLIF(LTRIM(RTRIM(wf.RepositoryId)), N''''))
FROM workflow.[' + @TableName + N'] a
INNER JOIN workflow.Workflows wf ON wf.Id = @WorkflowId AND wf.IsDeleted = 0
WHERE a.RepositoryId IS NULL
  AND TRY_CONVERT(UNIQUEIDENTIFIER, NULLIF(LTRIM(RTRIM(wf.RepositoryId)), N'''')) IS NOT NULL;';
EXEC sp_executesql @Sql, N'@WorkflowId UNIQUEIDENTIFIER', @WorkflowId = @WorkflowId;
PRINT 'Backfilled RepositoryId from workflow.Workflows.RepositoryId.';

PRINT 'Finished workflow.[' + @TableName + '].';
GO
