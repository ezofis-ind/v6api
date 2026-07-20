-- Tenant: Email ingest mailbox config + processed message dedup
-- Run against tenant database (or auto-created by EmailIngestService.EnsureSchemaAsync).

IF OBJECT_ID(N'dbo.EmailIngestMailbox', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.EmailIngestMailbox (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_EmailIngestMailbox PRIMARY KEY,
        ConnectorId UNIQUEIDENTIFIER NOT NULL,
        WorkflowId UNIQUEIDENTIFIER NOT NULL,
        IsEnabled BIT NOT NULL CONSTRAINT DF_EmailIngestMailbox_IsEnabled DEFAULT (1),
        PollIntervalMinutes INT NOT NULL CONSTRAINT DF_EmailIngestMailbox_PollInterval DEFAULT (5),
        QueryFilter NVARCHAR(512) NULL,
        MasterSource NVARCHAR(32) NOT NULL CONSTRAINT DF_EmailIngestMailbox_MasterSource DEFAULT (N'InternalForm'),
        MasterFormId NVARCHAR(128) NULL,
        MasterConnectorId UNIQUEIDENTIFIER NULL,
        AttachmentExtensions NVARCHAR(256) NOT NULL CONSTRAINT DF_EmailIngestMailbox_Ext DEFAULT (N'.pdf,.png,.jpg,.jpeg,.tif,.tiff'),
        LastPolledAtUtc DATETIME2(3) NULL,
        LastError NVARCHAR(2000) NULL,
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_EmailIngestMailbox_Created DEFAULT (SYSUTCDATETIME()),
        ModifiedAtUtc DATETIME2(3) NULL,
        CreatedBy UNIQUEIDENTIFIER NULL,
        ModifiedBy UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL CONSTRAINT DF_EmailIngestMailbox_IsDeleted DEFAULT (0)
    );

    CREATE INDEX IX_EmailIngestMailbox_Enabled ON dbo.EmailIngestMailbox (IsEnabled, IsDeleted) WHERE IsDeleted = 0;
END
GO

IF OBJECT_ID(N'dbo.EmailIngestProcessed', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.EmailIngestProcessed (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_EmailIngestProcessed PRIMARY KEY,
        MailboxId UNIQUEIDENTIFIER NOT NULL,
        ProviderMessageId NVARCHAR(256) NOT NULL,
        AttachmentId NVARCHAR(256) NOT NULL,
        WorkflowInstanceId UNIQUEIDENTIFIER NULL,
        ProcessedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_EmailIngestProcessed_At DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT UQ_EmailIngestProcessed UNIQUE (MailboxId, ProviderMessageId, AttachmentId)
    );

    CREATE INDEX IX_EmailIngestProcessed_Mailbox ON dbo.EmailIngestProcessed (MailboxId, ProcessedAtUtc DESC);
END
GO
