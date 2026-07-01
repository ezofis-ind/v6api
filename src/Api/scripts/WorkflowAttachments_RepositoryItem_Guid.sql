/*
  WorkflowAttachments_{suffix} — RepositoryId & ItemId as UNIQUEIDENTIFIER (GUID)

  NOTE: On API create/publish/start workflow, tables are created automatically with GUID columns.
        See: Create_WorkflowAttachments_OnWorkflowCreate.sql (manual)
        Code: WorkflowTableCreator.GenerateAttachmentsTableScript

  For INT -> UNIQUEIDENTIFIER on existing tables only, use:
        Alter_WorkflowAttachments_IntToUniqueIdentifier.sql

  Run against your TENANT database (not catalog).
*/

SET NOCOUNT ON;

-- ========== SET YOUR WORKFLOW ID HERE ==========
DECLARE @WorkflowId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000000'; -- replace
-- ===============================================

DECLARE @Suffix NVARCHAR(8) = LEFT(REPLACE(CAST(@WorkflowId AS NVARCHAR(36)), '-', ''), 8);
DECLARE @TableName SYSNAME = N'WorkflowAttachments_' + @Suffix;
DECLARE @TableFullName NVARCHAR(256) = N'workflow.' + QUOTENAME(@TableName);
DECLARE @Sql NVARCHAR(MAX);

IF @Suffix IS NULL OR LEN(@Suffix) < 8
BEGIN
    RAISERROR('Invalid @WorkflowId — cannot derive 8-char suffix.', 16, 1);
    RETURN;
END

PRINT 'WorkflowId: ' + CAST(@WorkflowId AS NVARCHAR(36));
PRINT 'Suffix:     ' + @Suffix;
PRINT 'Table:      ' + @TableFullName;

-- ---------------------------------------------------------------------------
-- 1) CREATE TABLE (new workflow) — RepositoryId & ItemId are GUID
-- ---------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM sys.tables t
    INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
    WHERE s.name = N'workflow' AND t.name = @TableName)
BEGIN
    SET @Sql = N'
    CREATE TABLE workflow.[' + @TableName + N'] (
        Id                 UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_' + @TableName + N' PRIMARY KEY DEFAULT NEWID(),
        TenantId           UNIQUEIDENTIFIER NOT NULL,
        WorkflowInstanceId UNIQUEIDENTIFIER NOT NULL,
        StepInstanceId     UNIQUEIDENTIFIER NULL,
        RepositoryId       UNIQUEIDENTIFIER NULL,
        ItemId               UNIQUEIDENTIFIER NULL,
        FormJsonId           NVARCHAR(128) NULL,
        FileName             NVARCHAR(512) NULL,
        FilePath             NVARCHAR(1024) NULL,
        FileSize             BIGINT NULL,
        ContentType          NVARCHAR(128) NULL,
        CreatedAtUtc         DATETIME2 NOT NULL CONSTRAINT DF_' + @TableName + N'_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        ModifiedAtUtc        DATETIME2 NULL,
        CreatedBy            UNIQUEIDENTIFIER NOT NULL,
        ModifiedBy           UNIQUEIDENTIFIER NULL,
        IsDeleted            BIT NOT NULL CONSTRAINT DF_' + @TableName + N'_IsDeleted DEFAULT (0)
    );

    CREATE NONCLUSTERED INDEX IX_' + @TableName + N'_Tenant_Instance
        ON workflow.[' + @TableName + N'] (TenantId, WorkflowInstanceId, IsDeleted);
    ';
    EXEC sp_executesql @Sql;
    PRINT 'Created table with RepositoryId / ItemId as UNIQUEIDENTIFIER.';
END
ELSE
    PRINT 'Table already exists — running column upgrade / backfill only.';

-- ---------------------------------------------------------------------------
-- 2) UPGRADE existing table: INT (or wrong type) -> UNIQUEIDENTIFIER
--    Safe when columns are missing or already UNIQUEIDENTIFIER.
-- ---------------------------------------------------------------------------
DECLARE @RepoType NVARCHAR(128);
DECLARE @ItemType NVARCHAR(128);

SELECT @RepoType = ty.name
FROM sys.columns c
INNER JOIN sys.types ty ON ty.user_type_id = c.user_type_id
INNER JOIN sys.tables t ON t.object_id = c.object_id
INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE s.name = N'workflow' AND t.name = @TableName AND c.name = N'RepositoryId';

SELECT @ItemType = ty.name
FROM sys.columns c
INNER JOIN sys.types ty ON ty.user_type_id = c.user_type_id
INNER JOIN sys.tables t ON t.object_id = c.object_id
INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE s.name = N'workflow' AND t.name = @TableName AND c.name = N'ItemId';

-- Add columns if missing
IF @RepoType IS NULL
BEGIN
    SET @Sql = N'ALTER TABLE workflow.[' + @TableName + N'] ADD RepositoryId UNIQUEIDENTIFIER NULL;';
    EXEC sp_executesql @Sql;
    PRINT 'Added RepositoryId UNIQUEIDENTIFIER.';
    SET @RepoType = N'uniqueidentifier';
END

IF @ItemType IS NULL
BEGIN
    SET @Sql = N'ALTER TABLE workflow.[' + @TableName + N'] ADD ItemId UNIQUEIDENTIFIER NULL;';
    EXEC sp_executesql @Sql;
    PRINT 'Added ItemId UNIQUEIDENTIFIER.';
    SET @ItemType = N'uniqueidentifier';
END

-- Convert INT -> UNIQUEIDENTIFIER (drops INT values — backfill RepositoryId from workflow link after)
IF @RepoType IN (N'int', N'bigint', N'smallint')
BEGIN
    SET @Sql = N'
    ALTER TABLE workflow.[' + @TableName + N'] DROP COLUMN RepositoryId;
    ALTER TABLE workflow.[' + @TableName + N'] ADD RepositoryId UNIQUEIDENTIFIER NULL;
    ';
    EXEC sp_executesql @Sql;
    PRINT 'RepositoryId changed from integer to UNIQUEIDENTIFIER.';
    SET @RepoType = N'uniqueidentifier';
END

IF @ItemType IN (N'int', N'bigint', N'smallint')
BEGIN
    SET @Sql = N'
    ALTER TABLE workflow.[' + @TableName + N'] DROP COLUMN ItemId;
    ALTER TABLE workflow.[' + @TableName + N'] ADD ItemId UNIQUEIDENTIFIER NULL;
    ';
    EXEC sp_executesql @Sql;
    PRINT 'ItemId changed from integer to UNIQUEIDENTIFIER.';
    SET @ItemType = N'uniqueidentifier';
END

-- Remove mistaken extra columns if someone added them earlier
IF EXISTS (
    SELECT 1 FROM sys.columns c
    INNER JOIN sys.tables t ON t.object_id = c.object_id
    INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
    WHERE s.name = N'workflow' AND t.name = @TableName AND c.name = N'RepositoryGuid')
BEGIN
    SET @Sql = N'ALTER TABLE workflow.[' + @TableName + N'] DROP COLUMN RepositoryGuid;';
    EXEC sp_executesql @Sql;
    PRINT 'Dropped unused column RepositoryGuid.';
END

IF EXISTS (
    SELECT 1 FROM sys.columns c
    INNER JOIN sys.tables t ON t.object_id = c.object_id
    INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
    WHERE s.name = N'workflow' AND t.name = @TableName AND c.name = N'ItemGuid')
BEGIN
    SET @Sql = N'ALTER TABLE workflow.[' + @TableName + N'] DROP COLUMN ItemGuid;';
    EXEC sp_executesql @Sql;
    PRINT 'Dropped unused column ItemGuid.';
END

-- ---------------------------------------------------------------------------
-- 3) BACKFILL ItemId from FormJsonId when it is a GUID string (with dashes)
--    For 32-char "N" format without dashes, fix in app or use the UPDATE below.
-- ---------------------------------------------------------------------------
SET @Sql = N'
UPDATE workflow.[' + @TableName + N']
SET ItemId = TRY_CONVERT(UNIQUEIDENTIFIER, FormJsonId)
WHERE ItemId IS NULL
  AND FormJsonId IS NOT NULL
  AND TRY_CONVERT(UNIQUEIDENTIFIER, FormJsonId) IS NOT NULL;
';
EXEC sp_executesql @Sql;

-- Optional: backfill 32-char N format FormJsonId -> ItemId (uncomment if needed)
/*
SET @Sql = N'
UPDATE a
SET ItemId = CONVERT(UNIQUEIDENTIFIER,
        STUFF(STUFF(STUFF(STUFF(a.FormJsonId, 9, 0, ''-''), 14, 0, ''-''), 19, 0, ''-''), 24, 0, ''-''))
FROM workflow.[' + @TableName + N'] a
WHERE a.ItemId IS NULL
  AND a.FormJsonId IS NOT NULL
  AND LEN(LTRIM(RTRIM(a.FormJsonId))) = 32
  AND a.FormJsonId NOT LIKE ''%[^0-9A-Fa-f]%'';
';
EXEC sp_executesql @Sql;
*/

-- Backfill RepositoryId from workflow.Workflows.RepositoryId link (GUID string)
SET @Sql = N'
UPDATE a
SET a.RepositoryId = w.RepoGuid
FROM workflow.[' + @TableName + N'] a
CROSS APPLY (
    SELECT TRY_CONVERT(UNIQUEIDENTIFIER, NULLIF(LTRIM(RTRIM(wf.RepositoryId)), N'''')) AS RepoGuid
    FROM workflow.Workflows wf
    WHERE wf.Id = @WorkflowId AND wf.IsDeleted = 0
) w
WHERE a.RepositoryId IS NULL
  AND w.RepoGuid IS NOT NULL;
';
EXEC sp_executesql @Sql, N'@WorkflowId UNIQUEIDENTIFIER', @WorkflowId = @WorkflowId;

PRINT 'Done.';
GO

-- =============================================================================
-- BONUS: Fix ALL workflow.WorkflowAttachments_* tables in this database
-- (run separately if you have many workflows)
-- =============================================================================
/*
DECLARE @Table SYSNAME, @Sql NVARCHAR(MAX);

DECLARE c CURSOR LOCAL FAST_FORWARD FOR
SELECT t.name
FROM sys.tables t
INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE s.name = N'workflow'
  AND t.name LIKE N'WorkflowAttachments[_]%' ESCAPE '\';

OPEN c;
FETCH NEXT FROM c INTO @Table;

WHILE @@FETCH_STATUS = 0
BEGIN
    -- same INT->GUID logic per table (abbreviated)
    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'workflow.' + @Table) AND name = N'RepositoryId' AND system_type_id = 56) -- int
    BEGIN
        SET @Sql = N'ALTER TABLE workflow.[' + @Table + N'] DROP COLUMN RepositoryId;
                   ALTER TABLE workflow.[' + @Table + N'] ADD RepositoryId UNIQUEIDENTIFIER NULL;';
        EXEC sp_executesql @Sql;
    END
    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'workflow.' + @Table) AND name = N'ItemId' AND system_type_id = 56)
    BEGIN
        SET @Sql = N'ALTER TABLE workflow.[' + @Table + N'] DROP COLUMN ItemId;
                   ALTER TABLE workflow.[' + @Table + N'] ADD ItemId UNIQUEIDENTIFIER NULL;';
        EXEC sp_executesql @Sql;
    END
    FETCH NEXT FROM c INTO @Table;
END

CLOSE c;
DEALLOCATE c;
*/
