-- Add ActivityId / StageType to all per-workflow WorkflowStepInstances_* tables (idempotent).
DECLARE @sql NVARCHAR(MAX) = N'';

SELECT @sql = @sql + N'
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N''workflow.' + t.name + N''') AND name = N''ActivityId'')
    ALTER TABLE workflow.[' + t.name + N'] ADD ActivityId NVARCHAR(128) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N''workflow.' + t.name + N''') AND name = N''StageType'')
    ALTER TABLE workflow.[' + t.name + N'] ADD StageType NVARCHAR(64) NULL;
'
FROM sys.tables t
INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
WHERE s.name = N'workflow' AND t.name LIKE N'WorkflowStepInstances[_]%';

IF LEN(@sql) > 0
    EXEC sp_executesql @sql;

PRINT 'WorkflowStepInstances_* ActivityId/StageType columns ensured.';
GO
