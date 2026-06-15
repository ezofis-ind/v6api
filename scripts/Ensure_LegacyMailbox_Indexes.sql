/*
  Add list-performance indexes on all workflow.Inbox_* / Sent_* / Completed_* tables.
  Run on TENANT database (safe to re-run).
*/

SET NOCOUNT ON;

DECLARE @Table SYSNAME, @Idx SYSNAME, @Sql NVARCHAR(MAX);

DECLARE c CURSOR LOCAL FAST_FORWARD FOR
SELECT t.name
FROM sys.tables t
INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE s.name = N'workflow'
  AND (t.name LIKE N'Inbox[_]%' ESCAPE '\'
    OR t.name LIKE N'Sent[_]%' ESCAPE '\'
    OR t.name LIKE N'Completed[_]%' ESCAPE '\');

OPEN c;
FETCH NEXT FROM c INTO @Table;
WHILE @@FETCH_STATUS = 0
BEGIN
    DECLARE @IdxUser SYSNAME = N'IX_' + @Table + N'_User_Created';
    SET @Idx = N'IX_' + @Table + N'_Instance_Created';
    SET @Sql = N'
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = @Idx AND object_id = OBJECT_ID(N''workflow.[' + @Table + N']''))
    CREATE NONCLUSTERED INDEX [' + @Idx + N']
    ON workflow.[' + @Table + N'] (workflowInstanceId, transaction_createdAt DESC, id DESC)
    INCLUDE (userId, transaction_createdBy, transactionId, name, referenceNumber, stage, review);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = @IdxUser AND object_id = OBJECT_ID(N''workflow.[' + @Table + N']''))
    CREATE NONCLUSTERED INDEX [' + @IdxUser + N']
    ON workflow.[' + @Table + N'] (userId, transaction_createdAt DESC, id DESC)
    INCLUDE (transaction_createdBy, workflowInstanceId, transactionId, name, referenceNumber);';
    EXEC sp_executesql @Sql, N'@Idx SYSNAME, @IdxUser SYSNAME', @Idx = @Idx, @IdxUser = @IdxUser;
    FETCH NEXT FROM c INTO @Table;
END
CLOSE c;
DEALLOCATE c;

PRINT 'Index ensure complete.';
GO
