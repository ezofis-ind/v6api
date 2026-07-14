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

IF NOT EXISTS (
    SELECT 1 FROM sys.tables t
    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE t.name = N'EventLogs' AND s.name = N'activitylog')
BEGIN
    CREATE TABLE activitylog.EventLogs (
        Id               uniqueidentifier NOT NULL CONSTRAINT PK_EventLogs PRIMARY KEY,
        TenantId         uniqueidentifier NOT NULL,
        UserId           uniqueidentifier NULL,
        UserDisplayName  nvarchar(256) NULL,
        UserEmail        nvarchar(256) NULL,
        EventTitle       nvarchar(512) NOT NULL,
        EventType        nvarchar(128) NOT NULL,
        Category         nvarchar(64) NOT NULL,
        Severity         nvarchar(32) NOT NULL,
        IpAddress        nvarchar(64) NULL,
        HttpMethod       nvarchar(10) NULL,
        Path             nvarchar(512) NULL,
        StatusCode       int NULL,
        CorrelationId    nvarchar(64) NULL,
        CreatedAtUtc     datetime2 NOT NULL
    );

    CREATE INDEX IX_EventLogs_TenantId_CreatedAtUtc
        ON activitylog.EventLogs (TenantId, CreatedAtUtc DESC);

    CREATE INDEX IX_EventLogs_TenantId_Category_CreatedAtUtc
        ON activitylog.EventLogs (TenantId, Category, CreatedAtUtc DESC);

    CREATE INDEX IX_EventLogs_TenantId_Severity
        ON activitylog.EventLogs (TenantId, Severity);

    PRINT 'activitylog.EventLogs created';
END
GO
