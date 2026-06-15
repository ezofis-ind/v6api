-- AP Agent job progress (Python PATCH + UI polling). Run on tenant database.
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'workflow')
    EXEC(N'CREATE SCHEMA workflow');
GO

IF OBJECT_ID(N'workflow.ApAgentJobProgress', N'U') IS NOT NULL
   AND COL_LENGTH(N'workflow.ApAgentJobProgress', N'ProgressPercent') IS NULL
BEGIN
    -- Drop legacy/broken table (e.g. failed create with reserved column name Percent)
    DROP TABLE workflow.ApAgentJobProgress;
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.tables t
    INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
    WHERE s.name = N'workflow' AND t.name = N'ApAgentJobProgress')
BEGIN
    CREATE TABLE workflow.ApAgentJobProgress (
        JobId             NVARCHAR(64)      NOT NULL PRIMARY KEY,
        TenantId          UNIQUEIDENTIFIER  NOT NULL,
        WorkflowId        UNIQUEIDENTIFIER  NOT NULL,
        InstanceId        UNIQUEIDENTIFIER  NOT NULL,
        HangfireState     NVARCHAR(32)      NULL,
        Stage             NVARCHAR(64)      NULL,
        Message           NVARCHAR(2000)    NULL,
        ProgressPercent   INT               NULL,
        ErrorMessage      NVARCHAR(MAX)     NULL,
        CreatedAtUtc      DATETIME2         NOT NULL CONSTRAINT DF_ApAgentJobProgress_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc      DATETIME2         NOT NULL CONSTRAINT DF_ApAgentJobProgress_UpdatedAt DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes i
    INNER JOIN sys.tables t ON t.object_id = i.object_id
    INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
    WHERE s.name = N'workflow'
      AND t.name = N'ApAgentJobProgress'
      AND i.name = N'IX_ApAgentJobProgress_InstanceId_Updated')
BEGIN
    CREATE INDEX IX_ApAgentJobProgress_InstanceId_Updated
        ON workflow.ApAgentJobProgress (InstanceId, UpdatedAtUtc DESC);
END
GO
