-- Run on each tenant database (e.g. ezofis_Tenant_xxx) if workflow create fails with:
-- Invalid object name 'workflow.WorkflowInitiateInfo'

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'workflow')
    EXEC('CREATE SCHEMA workflow');
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'WorkflowInitiateInfo' AND schema_id = SCHEMA_ID('workflow'))
BEGIN
    CREATE TABLE workflow.WorkflowInitiateInfo (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        WorkflowId UNIQUEIDENTIFIER NOT NULL,
        InputType NVARCHAR(256) NOT NULL,
        InputJson NVARCHAR(MAX) NULL,
        Status INT NOT NULL DEFAULT 0,
        Remarks NVARCHAR(2000) NOT NULL DEFAULT '',
        CreatedBy UNIQUEIDENTIFIER NOT NULL,
        CreatedAtUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        RepositoryId INT NULL
    );
    CREATE INDEX IX_WorkflowInitiateInfo_WorkflowId ON workflow.WorkflowInitiateInfo (WorkflowId);
    CREATE INDEX IX_WorkflowInitiateInfo_TenantId_WorkflowId ON workflow.WorkflowInitiateInfo (TenantId, WorkflowId);
    PRINT 'Created workflow.WorkflowInitiateInfo';
END
ELSE
    PRINT 'workflow.WorkflowInitiateInfo already exists';
GO
