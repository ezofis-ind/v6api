-- Run on each tenant database.
-- Adds mailbox columns for already-created workflow.Inbox_*, workflow.Sent_*, workflow.Completed_* tables.
-- Safe to re-run (checks existence before ALTER).

SET NOCOUNT ON;

DECLARE @schemaName SYSNAME = N'workflow';
DECLARE @tableName SYSNAME;
DECLARE @sql NVARCHAR(MAX);

DECLARE mailbox_cursor CURSOR FAST_FORWARD FOR
SELECT t.name
FROM sys.tables t
INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE s.name = @schemaName
  AND (
        t.name LIKE N'Inbox[_]%' OR
        t.name LIKE N'Sent[_]%' OR
        t.name LIKE N'Completed[_]%'
      );

OPEN mailbox_cursor;
FETCH NEXT FROM mailbox_cursor INTO @tableName;

WHILE @@FETCH_STATUS = 0
BEGIN
    SET @sql = N'
IF COL_LENGTH(''' + @schemaName + N'.' + @tableName + N''', ''repositoryId'') IS NULL
    ALTER TABLE [' + @schemaName + N'].[' + @tableName + N'] ADD repositoryId NVARCHAR(255) NULL;

IF COL_LENGTH(''' + @schemaName + N'.' + @tableName + N''', ''itemId'') IS NULL
    ALTER TABLE [' + @schemaName + N'].[' + @tableName + N'] ADD itemId NVARCHAR(255) NULL;

IF COL_LENGTH(''' + @schemaName + N'.' + @tableName + N''', ''formId'') IS NULL
    ALTER TABLE [' + @schemaName + N'].[' + @tableName + N'] ADD formId NVARCHAR(255) NULL;

IF COL_LENGTH(''' + @schemaName + N'.' + @tableName + N''', ''formEntryId'') IS NULL
    ALTER TABLE [' + @schemaName + N'].[' + @tableName + N'] ADD formEntryId NVARCHAR(255) NULL;

IF COL_LENGTH(''' + @schemaName + N'.' + @tableName + N''', ''formData'') IS NULL
    ALTER TABLE [' + @schemaName + N'].[' + @tableName + N'] ADD formData NVARCHAR(MAX) NULL;
';

    EXEC sp_executesql @sql;
    PRINT N'Updated: [' + @schemaName + N'].[' + @tableName + N']';

    FETCH NEXT FROM mailbox_cursor INTO @tableName;
END

CLOSE mailbox_cursor;
DEALLOCATE mailbox_cursor;

-- Verify columns on mailbox tables
SELECT
    s.name AS SchemaName,
    t.name AS TableName,
    c.name AS ColumnName,
    ty.name AS DataType,
    c.max_length AS MaxLength
FROM sys.tables t
INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
INNER JOIN sys.columns c ON c.object_id = t.object_id
INNER JOIN sys.types ty ON ty.user_type_id = c.user_type_id
WHERE s.name = N'workflow'
  AND (t.name LIKE N'Inbox[_]%' OR t.name LIKE N'Sent[_]%' OR t.name LIKE N'Completed[_]%')
  AND c.name IN (N'repositoryId', N'itemId', N'formId', N'formEntryId', N'formData')
ORDER BY t.name, c.name;
