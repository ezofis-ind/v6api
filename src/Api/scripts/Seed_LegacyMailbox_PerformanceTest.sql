/*
  Bulk seed Inbox / Sent / Completed for performance testing (e.g. 10,000 rows each).

  Run on TENANT database after workflow tables exist.
  Uses same column shapes as WorkflowLegacyMailboxSyncService (workflowInstanceId = D-format GUID string).

  SET variables below, then run sections 1 (indexes), 2 (seed), 3 (verify).
*/

SET NOCOUNT ON;

-- ========== CONFIGURE ==========
DECLARE @WorkflowId          UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000000'; -- your workflow id
DECLARE @WorkflowInstanceId  UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001'; -- your instance id
DECLARE @UserId              UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000002'; -- current user (D format in rows)
DECLARE @RowsPerTable        INT = 10000;
-- ===============================

DECLARE @Suffix           NVARCHAR(8)  = LEFT(REPLACE(CAST(@WorkflowId AS NVARCHAR(36)), '-', ''), 8);
DECLARE @WorkflowIdStr    NVARCHAR(36) = CAST(@WorkflowId AS NVARCHAR(36));
DECLARE @InstanceIdStr    NVARCHAR(36) = CAST(@WorkflowInstanceId AS NVARCHAR(36));
DECLARE @UserIdStr        NVARCHAR(100) = CAST(@UserId AS NVARCHAR(36));
DECLARE @InboxTable       SYSNAME = N'Inbox_' + @Suffix;
DECLARE @SentTable        SYSNAME = N'Sent_' + @Suffix;
DECLARE @CompletedTable   SYSNAME = N'Completed_' + @Suffix;
DECLARE @Sql              NVARCHAR(MAX);

PRINT 'WorkflowId: ' + @WorkflowIdStr;
PRINT 'InstanceId: ' + @InstanceIdStr;
PRINT 'Suffix:     ' + @Suffix;

-- ---------------------------------------------------------------------------
-- 1) Indexes (fast list: workflowInstanceId + transaction_createdAt)
-- ---------------------------------------------------------------------------
DECLARE @Prefix SYSNAME, @Table SYSNAME, @Idx SYSNAME;
DECLARE idx CURSOR LOCAL FAST_FORWARD FOR
    SELECT v.Prefix, v.TableName
    FROM (VALUES
        (N'Inbox', @InboxTable),
        (N'Sent', @SentTable),
        (N'Completed', @CompletedTable)) v(Prefix, TableName);

OPEN idx;
FETCH NEXT FROM idx INTO @Prefix, @Table;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @Idx = N'IX_' + @Table + N'_Instance_Created';
    SET @Sql = N'
IF EXISTS (SELECT 1 FROM sys.tables WHERE name = @Table AND schema_id = SCHEMA_ID(N''workflow''))
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = @Idx AND object_id = OBJECT_ID(N''workflow.[' + @Table + N']''))
BEGIN
    CREATE NONCLUSTERED INDEX [' + @Idx + N']
    ON workflow.[' + @Table + N'] (workflowInstanceId, transaction_createdAt DESC, id DESC)
    INCLUDE (userId, transaction_createdBy, transactionId, name, referenceNumber);
    PRINT ''Created index '' + @Idx;
END';
    EXEC sp_executesql @Sql, N'@Table SYSNAME, @Idx SYSNAME', @Table = @Table, @Idx = @Idx;
    FETCH NEXT FROM idx INTO @Prefix, @Table;
END
CLOSE idx;
DEALLOCATE idx;

-- ---------------------------------------------------------------------------
-- 2) Seed helper: insert @RowsPerTable into one mailbox table
-- ---------------------------------------------------------------------------
DECLARE @TargetTable SYSNAME;
DECLARE @MailboxKind NVARCHAR(32);

DECLARE seed CURSOR LOCAL FAST_FORWARD FOR
    SELECT TableName, Kind FROM (VALUES
        (@InboxTable, N'Inbox'),
        (@SentTable, N'Sent'),
        (@CompletedTable, N'Completed')) v(TableName, Kind);

OPEN seed;
FETCH NEXT FROM seed INTO @TargetTable, @MailboxKind;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = @TargetTable AND schema_id = SCHEMA_ID(N'workflow'))
    BEGIN
        RAISERROR('Table workflow.%s does not exist. Create workflow or run provision-tables first.', 16, 1, @TargetTable);
        RETURN;
    END

    PRINT 'Seeding ' + @TargetTable + ' (' + CAST(@RowsPerTable AS NVARCHAR(20)) + ' rows)...';

    SET @Sql = N'
;WITH tally AS (
    SELECT TOP (@Rows) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
    FROM sys.all_objects a
    CROSS JOIN sys.all_objects b
)
INSERT INTO workflow.[' + @TargetTable + N'] (
    userId, groupId, workflowId, name, workflowInstanceId, referenceNumber,
    createdAtUtc, startedAtUtc, completedAtUtc, context,
    transactionId, activityId, ruleId, stageType, stage, review,
    transaction_createdAt, transaction_createdBy, transaction_createdByEmail,
    transaction_modifiedAt, transaction_modifiedBy,
    formId, formEntryId, commentsCount, attachmentCount, createdByName
)
SELECT
    @UserIdStr,
    NULL,
    @WorkflowIdStr,
  N''Perf Test '' + @MailboxKind,
    @InstanceIdStr,
    N''REF-'' + RIGHT(''000000'' + CAST(n AS NVARCHAR(10)), 6),
    DATEADD(SECOND, -n, SYSUTCDATETIME()),
    DATEADD(SECOND, -n, SYSUTCDATETIME()),
    CASE WHEN @MailboxKind = N''Completed'' THEN DATEADD(SECOND, -n, SYSUTCDATETIME()) ELSE NULL END,
    N''load test'',
    CAST(NEWID() AS NVARCHAR(36)),
    N''act-'' + CAST(n AS NVARCHAR(12)),
    NULL,
    CASE WHEN @MailboxKind = N''Inbox'' THEN N''USER'' WHEN @MailboxKind = N''Sent'' THEN N''PROCESS'' ELSE N''END'' END,
    N''Stage '' + CAST((n % 5) + 1 AS NVARCHAR(10)),
    N''pending'',
    DATEADD(SECOND, -n, GETDATE()),
    @UserIdStr,
    N''user@example.com'',
    GETDATE(),
    @UserIdStr,
    NULL,
    CAST(n AS NVARCHAR(32)),
    0,
    0,
    N''Seed User''
FROM tally;
';
    EXEC sp_executesql @Sql,
        N'@Rows INT, @UserIdStr NVARCHAR(100), @WorkflowIdStr NVARCHAR(36), @InstanceIdStr NVARCHAR(36), @MailboxKind NVARCHAR(32)',
        @Rows = @RowsPerTable,
        @UserIdStr = @UserIdStr,
        @WorkflowIdStr = @WorkflowIdStr,
        @InstanceIdStr = @InstanceIdStr,
        @MailboxKind = @MailboxKind;

    FETCH NEXT FROM seed INTO @TargetTable, @MailboxKind;
END
CLOSE seed;
DEALLOCATE seed;

-- ---------------------------------------------------------------------------
-- 3) Verify counts for this instance + user
-- ---------------------------------------------------------------------------
SET @Sql = N'
SELECT @InboxTable AS [Table], COUNT(*) AS Cnt
FROM workflow.[' + @InboxTable + N']
WHERE workflowInstanceId = @InstanceIdStr
  AND (userId = @UserIdStr OR transaction_createdBy = @UserIdStr)
UNION ALL
SELECT @SentTable, COUNT(*)
FROM workflow.[' + @SentTable + N']
WHERE workflowInstanceId = @InstanceIdStr
  AND (userId = @UserIdStr OR transaction_createdBy = @UserIdStr)
UNION ALL
SELECT @CompletedTable, COUNT(*)
FROM workflow.[' + @CompletedTable + N']
WHERE workflowInstanceId = @InstanceIdStr
  AND (userId = @UserIdStr OR transaction_createdBy = @UserIdStr);';

EXEC sp_executesql @Sql,
    N'@InboxTable SYSNAME, @SentTable SYSNAME, @CompletedTable SYSNAME, @InstanceIdStr NVARCHAR(36), @UserIdStr NVARCHAR(100)',
    @InboxTable = @InboxTable, @SentTable = @SentTable, @CompletedTable = @CompletedTable,
    @InstanceIdStr = @InstanceIdStr, @UserIdStr = @UserIdStr;

PRINT 'Done. Test API:';
PRINT '  GET /api/workflows/inbox?workflowId=...&instanceId=...&pageNumber=1&pageSize=50&skipTotal=true';
GO
