/*
  Load-test seed: Inbox / Sent / Completed for workflow F001E07B-DD0C-4687-A0E2-F307086FC6F9
  Run on TENANT database (not catalog).

  - 10,000 unique workflowInstanceId per mailbox (30,000 instances total if all three enabled)
  - Matching workflow.transaction_F001E07B rows so API filters work (ActionStatus / END)
  - referenceNumber prefix LOADTEST- for easy cleanup

  BEFORE RUN:
  1. Set @TestUserId to the GUID you use in JWT (same as inbox API tests).
  2. Ensure tables exist (start workflow once or run app so EnsureLegacyMailboxTables runs):
        workflow.Inbox_F001E07B, workflow.Sent_F001E07B, workflow.Completed_F001E07B,
        workflow.transaction_F001E07B
  3. Adjust @RecordsPerMailbox (default 10000).

  CLEANUP (optional, bottom of script):
        DELETE rows WHERE referenceNumber LIKE N'LOADTEST-%'
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @WorkflowId UNIQUEIDENTIFIER = 'F001E07B-DD0C-4687-A0E2-F307086FC6F9';
DECLARE @WorkflowIdStr NVARCHAR(36) = CONVERT(NVARCHAR(36), @WorkflowId);
DECLARE @Suffix NCHAR(8) = N'F001E07B';

-- *** SET THIS to your login user id (JWT sub / users.Users.Id) ***
DECLARE @TestUserId UNIQUEIDENTIFIER = '9483f673-416a-43c8-b293-83a72025af58';
DECLARE @TestUserIdStr NVARCHAR(36) = CONVERT(NVARCHAR(36), @TestUserId);

DECLARE @RecordsPerMailbox INT = 10000;
DECLARE @BatchSize INT = 2000;
DECLARE @Now DATETIME2 = SYSUTCDATETIME();
DECLARE @NowDt DATETIME = GETUTCDATE();

DECLARE @InboxTable SYSNAME = N'Inbox_F001E07B';
DECLARE @SentTable SYSNAME = N'Sent_F001E07B';
DECLARE @CompletedTable SYSNAME = N'Completed_F001E07B';
DECLARE @TxTable SYSNAME = N'transaction_F001E07B';

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE schema_id = SCHEMA_ID(N'workflow') AND name = @InboxTable)
BEGIN
    RAISERROR('Missing workflow.%s. Create via API (start workflow) or EnsureLegacyMailboxTables first.', 16, 1, @InboxTable);
    RETURN;
END;

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE schema_id = SCHEMA_ID(N'workflow') AND name = @TxTable)
BEGIN
    RAISERROR('Missing workflow.%s.', 16, 1, @TxTable);
    RETURN;
END;

PRINT N'Seeding load test for workflow ' + @WorkflowIdStr + N', user ' + @TestUserIdStr;
PRINT N'Records per mailbox: ' + CAST(@RecordsPerMailbox AS NVARCHAR(20));

IF OBJECT_ID(N'tempdb..#SeedBatch') IS NOT NULL
    DROP TABLE #SeedBatch;

CREATE TABLE #SeedBatch (
    n INT NOT NULL,
    InstanceId UNIQUEIDENTIFIER NOT NULL,
    RefNum NVARCHAR(64) NOT NULL,
    TransactionRowId INT NULL
);

/* ========== INBOX (ActionStatus = 0, not END) ========== */
PRINT N'--- Inbox ---';
DECLARE @InboxFrom INT = 1;
DECLARE @InboxTo INT;

WHILE @InboxFrom <= @RecordsPerMailbox
BEGIN
    SET @InboxTo = CASE WHEN @InboxFrom + @BatchSize - 1 > @RecordsPerMailbox
        THEN @RecordsPerMailbox ELSE @InboxFrom + @BatchSize - 1 END;

    TRUNCATE TABLE #SeedBatch;

    ;WITH nums AS (
        SELECT TOP (@InboxTo - @InboxFrom + 1)
            @InboxFrom - 1 + ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
        FROM sys.all_objects a
        CROSS JOIN sys.all_objects b
    )
    INSERT INTO #SeedBatch (n, InstanceId, RefNum)
    SELECT
        n,
        NEWID(),
        CONCAT(N'LOADTEST-INBOX-', RIGHT(N'000000' + CAST(n AS NVARCHAR(10)), 6))
    FROM nums;

    INSERT INTO workflow.[transaction_F001E07B] (
        WorkflowInstanceId, ActivityId, StageType, StageName, Review, ActionStatus,
        ActivityUserId, CreatedAt, CreatedBy, IsDeleted)
    SELECT
        b.InstanceId,
        N'loadtest-ap-agent',
        N'AP_AGENT',
        N'Ap Agent',
        NULL,
        0,
        @TestUserId,
        @Now,
        @TestUserId,
        0
    FROM #SeedBatch b;

    UPDATE b
    SET b.TransactionRowId = tr.Id
    FROM #SeedBatch b
    INNER JOIN workflow.[transaction_F001E07B] tr
        ON tr.WorkflowInstanceId = b.InstanceId
       AND tr.IsDeleted = 0
       AND tr.ActionStatus = 0;

    INSERT INTO workflow.[Inbox_F001E07B] (
        userId, workflowId, name, workflowInstanceId, referenceNumber,
        createdAtUtc, startedAtUtc, transactionId, activityId, stageType, stage,
        transaction_createdAt, transaction_createdBy, transaction_createdByEmail,
        repositoryId, itemId, formId, formEntryId, formData, commentsCount, attachmentCount)
    SELECT
        @TestUserIdStr,
        @WorkflowIdStr,
        CONCAT(N'Load Test Inbox ', b.RefNum),
        CONVERT(NVARCHAR(36), b.InstanceId),
        b.RefNum,
        @Now,
        @Now,
        CAST(b.TransactionRowId AS NVARCHAR(32)),
        N'loadtest-ap-agent',
        N'AP_AGENT',
        N'Ap Agent',
        @NowDt,
        @TestUserIdStr,
        N'loadtest@ezofis.com',
        NULL,
        NULL,
        NULL,
        NULL,
        NULL,
        0,
        0
    FROM #SeedBatch b
    WHERE b.TransactionRowId IS NOT NULL;

    PRINT N'  Inbox batch ' + CAST(@InboxFrom AS NVARCHAR(20)) + N'..' + CAST(@InboxTo AS NVARCHAR(20));
    SET @InboxFrom = @InboxTo + 1;
END;

/* ========== SENT (ActionStatus = 1, not END on instance) ========== */
PRINT N'--- Sent ---';
DECLARE @SentFrom INT = 1;
DECLARE @SentTo INT;

WHILE @SentFrom <= @RecordsPerMailbox
BEGIN
    SET @SentTo = CASE WHEN @SentFrom + @BatchSize - 1 > @RecordsPerMailbox
        THEN @RecordsPerMailbox ELSE @SentFrom + @BatchSize - 1 END;

    TRUNCATE TABLE #SeedBatch;

    ;WITH nums AS (
        SELECT TOP (@SentTo - @SentFrom + 1)
            @SentFrom - 1 + ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
        FROM sys.all_objects a
        CROSS JOIN sys.all_objects b
    )
    INSERT INTO #SeedBatch (n, InstanceId, RefNum)
    SELECT
        n,
        NEWID(),
        CONCAT(N'LOADTEST-SENT-', RIGHT(N'000000' + CAST(n AS NVARCHAR(10)), 6))
    FROM nums;

    INSERT INTO workflow.[transaction_F001E07B] (
        WorkflowInstanceId, ActivityId, StageType, StageName, Review, ActionStatus,
        ActivityUserId, CreatedAt, CreatedBy, IsDeleted)
    SELECT
        b.InstanceId,
        N'loadtest-review',
        N'REVIEW',
        N'Review',
        N'Approve',
        1,
        @TestUserId,
        @Now,
        @TestUserId,
        0
    FROM #SeedBatch b;

    UPDATE b
    SET b.TransactionRowId = tr.Id
    FROM #SeedBatch b
    INNER JOIN workflow.[transaction_F001E07B] tr
        ON tr.WorkflowInstanceId = b.InstanceId
       AND tr.IsDeleted = 0
       AND tr.ActionStatus = 1;

    INSERT INTO workflow.[Sent_F001E07B] (
        userId, workflowId, name, workflowInstanceId, referenceNumber,
        createdAtUtc, startedAtUtc, transactionId, activityId, stageType, stage, review,
        transaction_createdAt, transaction_createdBy, transaction_createdByEmail,
        commentsCount, attachmentCount)
    SELECT
        @TestUserIdStr,
        @WorkflowIdStr,
        CONCAT(N'Load Test Sent ', b.RefNum),
        CONVERT(NVARCHAR(36), b.InstanceId),
        b.RefNum,
        @Now,
        @Now,
        CAST(b.TransactionRowId AS NVARCHAR(32)),
        N'loadtest-review',
        N'REVIEW',
        N'Review',
        N'Approve',
        @NowDt,
        @TestUserIdStr,
        N'loadtest@ezofis.com',
        0,
        0
    FROM #SeedBatch b
    WHERE b.TransactionRowId IS NOT NULL;

    PRINT N'  Sent batch ' + CAST(@SentFrom AS NVARCHAR(20)) + N'..' + CAST(@SentTo AS NVARCHAR(20));
    SET @SentFrom = @SentTo + 1;
END;

/* ========== COMPLETED (END stage on transaction) ========== */
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE schema_id = SCHEMA_ID(N'workflow') AND name = @CompletedTable)
BEGIN
    PRINT N'WARNING: workflow.Completed_F001E07B missing; skipping completed seed.';
END
ELSE
BEGIN
    PRINT N'--- Completed ---';
    DECLARE @DoneFrom INT = 1;
    DECLARE @DoneTo INT;

    WHILE @DoneFrom <= @RecordsPerMailbox
    BEGIN
        SET @DoneTo = CASE WHEN @DoneFrom + @BatchSize - 1 > @RecordsPerMailbox
            THEN @RecordsPerMailbox ELSE @DoneFrom + @BatchSize - 1 END;

        TRUNCATE TABLE #SeedBatch;

        ;WITH nums AS (
            SELECT TOP (@DoneTo - @DoneFrom + 1)
                @DoneFrom - 1 + ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
            FROM sys.all_objects a
            CROSS JOIN sys.all_objects b
        )
        INSERT INTO #SeedBatch (n, InstanceId, RefNum)
        SELECT
            n,
            NEWID(),
            CONCAT(N'LOADTEST-DONE-', RIGHT(N'000000' + CAST(n AS NVARCHAR(10)), 6))
        FROM nums;

        INSERT INTO workflow.[transaction_F001E07B] (
            WorkflowInstanceId, ActivityId, StageType, StageName, Review, ActionStatus,
            ActivityUserId, CreatedAt, CreatedBy, IsDeleted)
        SELECT
            b.InstanceId,
            N'loadtest-end',
            N'END',
            N'End',
            N'Complete',
            1,
            @TestUserId,
            @Now,
            @TestUserId,
            0
        FROM #SeedBatch b;

        UPDATE b
        SET b.TransactionRowId = tr.Id
        FROM #SeedBatch b
        INNER JOIN workflow.[transaction_F001E07B] tr
            ON tr.WorkflowInstanceId = b.InstanceId
           AND tr.IsDeleted = 0
           AND UPPER(LTRIM(RTRIM(ISNULL(tr.StageType, N'')))) = N'END';

        INSERT INTO workflow.[Completed_F001E07B] (
            userId, workflowId, name, workflowInstanceId, referenceNumber,
            createdAtUtc, startedAtUtc, completedAtUtc, transactionId, activityId, stageType, stage,
            transaction_createdAt, transaction_createdBy, transaction_createdByEmail,
            commentsCount, attachmentCount)
        SELECT
            @TestUserIdStr,
            @WorkflowIdStr,
            CONCAT(N'Load Test Completed ', b.RefNum),
            CONVERT(NVARCHAR(36), b.InstanceId),
            b.RefNum,
            @Now,
            @Now,
            @Now,
            CAST(b.TransactionRowId AS NVARCHAR(32)),
            N'loadtest-end',
            N'END',
            N'End',
            @NowDt,
            @TestUserIdStr,
            N'loadtest@ezofis.com',
            0,
            0
        FROM #SeedBatch b
        WHERE b.TransactionRowId IS NOT NULL;

        PRINT N'  Completed batch ' + CAST(@DoneFrom AS NVARCHAR(20)) + N'..' + CAST(@DoneTo AS NVARCHAR(20));
        SET @DoneFrom = @DoneTo + 1;
    END;
END;

/* ========== Counts ========== */
SELECT N'Inbox' AS [Table], COUNT(*) AS [Rows]
FROM workflow.[Inbox_F001E07B]
WHERE referenceNumber LIKE N'LOADTEST-INBOX-%'
UNION ALL
SELECT N'Sent', COUNT(*)
FROM workflow.[Sent_F001E07B]
WHERE referenceNumber LIKE N'LOADTEST-SENT-%'
UNION ALL
SELECT N'Completed', COUNT(*)
FROM workflow.[Completed_F001E07B]
WHERE referenceNumber LIKE N'LOADTEST-DONE-%'
UNION ALL
SELECT N'transaction (loadtest instances)', COUNT(DISTINCT WorkflowInstanceId)
FROM workflow.[transaction_F001E07B] tx
WHERE EXISTS (
    SELECT 1 FROM workflow.[Inbox_F001E07B] i
    WHERE i.workflowInstanceId = CONVERT(NVARCHAR(36), tx.WorkflowInstanceId)
      AND i.referenceNumber LIKE N'LOADTEST-%'
)
   OR EXISTS (
    SELECT 1 FROM workflow.[Sent_F001E07B] s
    WHERE s.workflowInstanceId = CONVERT(NVARCHAR(36), tx.WorkflowInstanceId)
      AND s.referenceNumber LIKE N'LOADTEST-%'
)
   OR EXISTS (
    SELECT 1 FROM workflow.[Completed_F001E07B] c
    WHERE c.workflowInstanceId = CONVERT(NVARCHAR(36), tx.WorkflowInstanceId)
      AND c.referenceNumber LIKE N'LOADTEST-%'
);

PRINT N'Done. Test APIs:';
PRINT N'  GET /api/workflows/inbox?workflowId=F001E07B-DD0C-4687-A0E2-F307086FC6F9&pageSize=100';
PRINT N'  GET /api/workflows/sent?workflowId=...';
PRINT N'  GET /api/workflows/completed?workflowId=...';

/*
-- ========== CLEANUP (uncomment to remove load-test data) ==========
DELETE FROM workflow.[Inbox_F001E07B] WHERE referenceNumber LIKE N'LOADTEST-%';
DELETE FROM workflow.[Sent_F001E07B] WHERE referenceNumber LIKE N'LOADTEST-%';
DELETE FROM workflow.[Completed_F001E07B] WHERE referenceNumber LIKE N'LOADTEST-%';

DELETE tx
FROM workflow.[transaction_F001E07B] tx
WHERE NOT EXISTS (
    SELECT 1 FROM workflow.[Inbox_F001E07B] m
    WHERE m.workflowInstanceId = CONVERT(NVARCHAR(36), tx.WorkflowInstanceId)
      AND m.referenceNumber NOT LIKE N'LOADTEST-%'
)
AND (
    EXISTS (SELECT 1 FROM workflow.[Inbox_F001E07B] i WHERE i.referenceNumber LIKE N'LOADTEST-%' AND i.workflowInstanceId = CONVERT(NVARCHAR(36), tx.WorkflowInstanceId))
    OR EXISTS (SELECT 1 FROM workflow.[Sent_F001E07B] s WHERE s.referenceNumber LIKE N'LOADTEST-%' AND s.workflowInstanceId = CONVERT(NVARCHAR(36), tx.WorkflowInstanceId))
    OR EXISTS (SELECT 1 FROM workflow.[Completed_F001E07B] c WHERE c.referenceNumber LIKE N'LOADTEST-%' AND c.workflowInstanceId = CONVERT(NVARCHAR(36), tx.WorkflowInstanceId))
);
PRINT N'Load-test data removed.';
*/
