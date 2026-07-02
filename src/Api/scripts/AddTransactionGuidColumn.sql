-- Add TransactionGuid to all workflow.transaction_* tables (idempotent).
DECLARE @sql NVARCHAR(MAX) = N'';

SELECT @sql = @sql + N'
IF COL_LENGTH(''workflow.' + t.name + ''', ''TransactionGuid'') IS NULL
    ALTER TABLE workflow.[' + t.name + N'] ADD TransactionGuid UNIQUEIDENTIFIER NULL;
'
FROM sys.tables t
INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
WHERE s.name = N'workflow' AND t.name LIKE N'transaction[_]%';

IF LEN(@sql) > 0
    EXEC sp_executesql @sql;

PRINT 'transaction_* TransactionGuid column ensured.';
GO
