/*
  Manual recreate: workflow.transaction_{suffix} only (no process_* / processForm_* tables).
  Used by start-workflow and move-next APIs.

  HOW TO GET {suffix}:
    Take your WorkflowId GUID, remove hyphens, use first 8 characters (lowercase).
    Example: A4D9EB06-1ECC-468A-8AAE-5A9B46C9E6E2  ->  a4d9eb06

  RUN ON: tenant database (e.g. ezofis_Tenant_7), NOT catalog.
*/

USE ezofis_Tenant_7;  -- << change to your tenant DB
GO

DECLARE @suffix NVARCHAR(8) = N'a4d9eb06';  -- << change: first 8 chars of WorkflowId (no hyphens)
DECLARE @idx NVARCHAR(16) = REPLACE(@suffix, N'-', N'_');

DECLARE @dropSql NVARCHAR(MAX) = N'
IF OBJECT_ID(N''workflow.[transaction_' + @suffix + N']'', N''U'') IS NOT NULL
    DROP TABLE workflow.[transaction_' + @suffix + N'];';
EXEC sp_executesql @dropSql;

DECLARE @createSql NVARCHAR(MAX) = N'
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id
               WHERE s.name = N''workflow'' AND t.name = N''transaction_' + @suffix + N''')
BEGIN
    CREATE TABLE workflow.[transaction_' + @suffix + N'] (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        TransactionGuid UNIQUEIDENTIFIER NULL,
        WorkflowInstanceId UNIQUEIDENTIFIER NOT NULL,
        ActivityId NVARCHAR(128) NULL,
        RuleId NVARCHAR(128) NULL,
        StageType NVARCHAR(64) NULL,
        StageName NVARCHAR(256) NULL,
        Review NVARCHAR(64) NULL,
        ActionStatus INT NOT NULL CONSTRAINT DF_transaction_' + @idx + N'_ActionStatus DEFAULT (0),
        ActivityUserId UNIQUEIDENTIFIER NULL,
        ActivityGroupId INT NULL,
        UserIds NVARCHAR(MAX) NULL,
        GroupIds NVARCHAR(MAX) NULL,
        SlaTransactionId INT NULL,
        InputFrom NVARCHAR(64) NULL,
        LevelId INT NULL,
        UserType NVARCHAR(64) NULL,
        JiraIssueJson NVARCHAR(MAX) NULL,
        MlPrediction NVARCHAR(MAX) NULL,
        MlCondition NVARCHAR(MAX) NULL,
        TicketLockedBy UNIQUEIDENTIFIER NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_transaction_' + @idx + N'_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CreatedBy UNIQUEIDENTIFIER NULL,
        ModifiedAt DATETIME2 NULL,
        ModifiedBy UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL CONSTRAINT DF_transaction_' + @idx + N'_IsDeleted DEFAULT (0)
    );
    CREATE NONCLUSTERED INDEX IX_transaction_' + @idx + N'_WorkflowInstanceId_IsDeleted
        ON workflow.[transaction_' + @suffix + N'] (WorkflowInstanceId, IsDeleted);
    CREATE NONCLUSTERED INDEX IX_transaction_' + @idx + N'_ActivityUser_ActionStatus
        ON workflow.[transaction_' + @suffix + N'] (ActivityUserId, ActionStatus);
    CREATE UNIQUE NONCLUSTERED INDEX IX_transaction_' + @idx + N'_TransactionGuid
        ON workflow.[transaction_' + @suffix + N'] (TransactionGuid)
        WHERE TransactionGuid IS NOT NULL;
END';
EXEC sp_executesql @createSql;
