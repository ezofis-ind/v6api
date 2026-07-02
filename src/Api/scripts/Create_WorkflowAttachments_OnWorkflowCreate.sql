/*
  Per-workflow attachment table — same as API on CREATE / PUBLISH / START workflow
  (WorkflowTableCreator.GenerateAttachmentsTableScript)

  Table: workflow.WorkflowAttachments_{suffix}
  Suffix: first 8 chars of workflow Id without dashes (N format).

  RepositoryId, ItemId = UNIQUEIDENTIFIER (GUID) — NOT int.

  Run on TENANT database when creating tables manually, or rely on API create workflow.
*/

SET NOCOUNT ON;

DECLARE @WorkflowId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000000'; -- <<< SET THIS

DECLARE @Suffix    NVARCHAR(8)  = LEFT(REPLACE(CAST(@WorkflowId AS NVARCHAR(36)), '-', ''), 8);
DECLARE @TableName SYSNAME      = N'WorkflowAttachments_' + @Suffix;
DECLARE @TableFull NVARCHAR(128) = N'workflow.[' + @TableName + N']';
DECLARE @TableQual NVARCHAR(128) = N'workflow.' + @TableName;
DECLARE @Sql       NVARCHAR(MAX);

IF LEN(@Suffix) < 8
BEGIN
    RAISERROR('Invalid @WorkflowId.', 16, 1);
    RETURN;
END

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'workflow')
    EXEC(N'CREATE SCHEMA workflow');

-- ========== CREATE (new workflow) ==========
IF NOT EXISTS (
    SELECT 1 FROM sys.tables t
    JOIN sys.schemas s ON s.schema_id = t.schema_id
    WHERE s.name = N'workflow' AND t.name = @TableName)
BEGIN
    SET @Sql = N'
    CREATE TABLE ' + @TableFull + N' (
        Id                 UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        TenantId           UNIQUEIDENTIFIER NOT NULL,
        WorkflowInstanceId UNIQUEIDENTIFIER NOT NULL,
        StepInstanceId     UNIQUEIDENTIFIER NULL,
        RepositoryId       UNIQUEIDENTIFIER NULL,
        ItemId             UNIQUEIDENTIFIER NULL,
        FormJsonId         NVARCHAR(128) NULL,
        FileName           NVARCHAR(512) NULL,
        FilePath           NVARCHAR(1024) NULL,
        FileSize           BIGINT NULL,
        ContentType        NVARCHAR(128) NULL,
        CreatedAtUtc       DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        ModifiedAtUtc      DATETIME2 NULL,
        CreatedBy          UNIQUEIDENTIFIER NOT NULL,
        ModifiedBy         UNIQUEIDENTIFIER NULL,
        IsDeleted          BIT NOT NULL DEFAULT 0
    );
    CREATE NONCLUSTERED INDEX IX_' + @TableName + N'_Tenant_Instance
        ON ' + @TableFull + N' (TenantId, WorkflowInstanceId, IsDeleted);';
    EXEC sp_executesql @Sql;
    PRINT 'Created ' + @TableQual + ' (RepositoryId / ItemId = UNIQUEIDENTIFIER).';
END
ELSE
BEGIN
    -- ========== UPGRADE existing table (INT -> GUID) ==========
    IF COL_LENGTH(@TableQual, 'RepositoryGuid') IS NOT NULL
    BEGIN
        SET @Sql = N'ALTER TABLE ' + @TableFull + N' DROP COLUMN RepositoryGuid;';
        EXEC sp_executesql @Sql;
    END
    IF COL_LENGTH(@TableQual, 'ItemGuid') IS NOT NULL
    BEGIN
        SET @Sql = N'ALTER TABLE ' + @TableFull + N' DROP COLUMN ItemGuid;';
        EXEC sp_executesql @Sql;
    END

    IF COL_LENGTH(@TableQual, 'RepositoryId') IS NULL
    BEGIN
        SET @Sql = N'ALTER TABLE ' + @TableFull + N' ADD RepositoryId UNIQUEIDENTIFIER NULL;';
        EXEC sp_executesql @Sql;
    END
    ELSE IF EXISTS (
        SELECT 1 FROM sys.columns c
        JOIN sys.types ty ON ty.user_type_id = c.user_type_id
        JOIN sys.tables t ON t.object_id = c.object_id
        JOIN sys.schemas s ON s.schema_id = t.schema_id
        WHERE s.name = N'workflow' AND t.name = @TableName
          AND c.name = N'RepositoryId' AND ty.name IN (N'int', N'bigint', N'smallint'))
    BEGIN
        SET @Sql = N'
        ALTER TABLE ' + @TableFull + N' DROP COLUMN RepositoryId;
        ALTER TABLE ' + @TableFull + N' ADD RepositoryId UNIQUEIDENTIFIER NULL;';
        EXEC sp_executesql @Sql;
        PRINT 'RepositoryId: INT -> UNIQUEIDENTIFIER.';
    END

    IF COL_LENGTH(@TableQual, 'ItemId') IS NULL
    BEGIN
        SET @Sql = N'ALTER TABLE ' + @TableFull + N' ADD ItemId UNIQUEIDENTIFIER NULL;';
        EXEC sp_executesql @Sql;
    END
    ELSE IF EXISTS (
        SELECT 1 FROM sys.columns c
        JOIN sys.types ty ON ty.user_type_id = c.user_type_id
        JOIN sys.tables t ON t.object_id = c.object_id
        JOIN sys.schemas s ON s.schema_id = t.schema_id
        WHERE s.name = N'workflow' AND t.name = @TableName
          AND c.name = N'ItemId' AND ty.name IN (N'int', N'bigint', N'smallint'))
    BEGIN
        SET @Sql = N'
        ALTER TABLE ' + @TableFull + N' DROP COLUMN ItemId;
        ALTER TABLE ' + @TableFull + N' ADD ItemId UNIQUEIDENTIFIER NULL;';
        EXEC sp_executesql @Sql;
        PRINT 'ItemId: INT -> UNIQUEIDENTIFIER.';
    END

    PRINT 'Table ' + @TableQual + ' already existed — upgrade applied if needed.';
END

PRINT 'Done. WorkflowId=' + CAST(@WorkflowId AS NVARCHAR(36)) + ', suffix=' + @Suffix;
GO
