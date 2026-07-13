IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'activitylog')
    EXEC('CREATE SCHEMA activitylog');
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.tables t
    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE t.name = N'ApiAccessLogs' AND s.name = N'activitylog')
BEGIN
    CREATE TABLE activitylog.ApiAccessLogs (
        Id              uniqueidentifier NOT NULL CONSTRAINT PK_ApiAccessLogs PRIMARY KEY,
        TenantId        uniqueidentifier NOT NULL,
        UserId          uniqueidentifier NULL,
        UserEmail       nvarchar(256) NULL,
        HttpMethod      nvarchar(10) NOT NULL,
        Path            nvarchar(512) NOT NULL,
        QueryString     nvarchar(1024) NULL,
        StatusCode      int NOT NULL,
        DurationMs      int NOT NULL,
        CorrelationId   nvarchar(64) NULL,
        ClientIp        nvarchar(64) NULL,
        UserAgent       nvarchar(512) NULL,
        CreatedAtUtc    datetime2 NOT NULL
    );

    CREATE INDEX IX_ApiAccessLogs_TenantId_CreatedAtUtc
        ON activitylog.ApiAccessLogs (TenantId, CreatedAtUtc DESC);

    CREATE INDEX IX_ApiAccessLogs_TenantId_UserId_CreatedAtUtc
        ON activitylog.ApiAccessLogs (TenantId, UserId, CreatedAtUtc DESC);

    CREATE INDEX IX_ApiAccessLogs_TenantId_Path
        ON activitylog.ApiAccessLogs (TenantId, Path);

    PRINT 'activitylog.ApiAccessLogs created';
END
GO
