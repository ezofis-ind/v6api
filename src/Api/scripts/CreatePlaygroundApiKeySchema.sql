IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'playgroundApiKeys' AND schema_id = SCHEMA_ID(N'dbo'))
BEGIN
    CREATE TABLE dbo.playgroundApiKeys (
        Id              UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_playgroundApiKeys PRIMARY KEY,
        Email           NVARCHAR(256) NOT NULL,
        ApiKey          NVARCHAR(128) NOT NULL,
        KeyLabel        NVARCHAR(100) NULL,
        ProtectedPassword NVARCHAR(512) NULL,
        CreatedAtUtc    DATETIME2 NOT NULL,
        ExpiresAtUtc    DATETIME2 NULL,
        IsActive        BIT NOT NULL CONSTRAINT DF_playgroundApiKeys_IsActive DEFAULT (1)
    );

    CREATE UNIQUE INDEX UX_playgroundApiKeys_ApiKey ON dbo.playgroundApiKeys (ApiKey);
    CREATE INDEX IX_playgroundApiKeys_Email_CreatedAtUtc ON dbo.playgroundApiKeys (Email, CreatedAtUtc DESC);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'playgroundApiUsageLog' AND schema_id = SCHEMA_ID(N'dbo'))
BEGIN
    CREATE TABLE dbo.playgroundApiUsageLog (
        Id              UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_playgroundApiUsageLog PRIMARY KEY,
        ApiKeyId        UNIQUEIDENTIFIER NOT NULL,
        ApiKey          NVARCHAR(128) NOT NULL,
        Email           NVARCHAR(256) NOT NULL,
        Endpoint        NVARCHAR(512) NOT NULL,
        HttpMethod      NVARCHAR(16) NOT NULL,
        StatusCode      INT NOT NULL,
        DurationMs      BIGINT NOT NULL,
        RequestedAtUtc  DATETIME2 NOT NULL
    );

    CREATE INDEX IX_playgroundApiUsageLog_ApiKeyId ON dbo.playgroundApiUsageLog (ApiKeyId);
    CREATE INDEX IX_playgroundApiUsageLog_Email_RequestedAtUtc ON dbo.playgroundApiUsageLog (Email, RequestedAtUtc DESC);
END
GO
