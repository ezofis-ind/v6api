-- Mailbox / transaction indexes for inbox-sent-completed list performance.
-- Run on tenant DB. Safe to re-run.

SET NOCOUNT ON;

DECLARE @schemaName SYSNAME = N'workflow';
DECLARE @tableName SYSNAME;
DECLARE @sql NVARCHAR(MAX);

DECLARE ix CURSOR FAST_FORWARD FOR
SELECT t.name
FROM sys.tables t
INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE s.name = @schemaName
  AND t.name LIKE N'transaction[_]%';

OPEN ix;
FETCH NEXT FROM ix INTO @tableName;

WHILE @@FETCH_STATUS = 0
BEGIN
    SET @sql = N'
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N''IX_' + @tableName + N'_Instance_Status''
      AND object_id = OBJECT_ID(N''workflow.[' + @tableName + N']''))
    CREATE NONCLUSTERED INDEX [IX_' + @tableName + N'_Instance_Status]
    ON workflow.[' + @tableName + N'] (WorkflowInstanceId, IsDeleted, ActionStatus)
    INCLUDE (ActivityUserId, CreatedBy, StageType, ActivityGroupId);';
    EXEC sp_executesql @sql;
    PRINT N'Index ensured: workflow.' + @tableName;

    FETCH NEXT FROM ix INTO @tableName;
END

CLOSE ix;
DEALLOCATE ix;
