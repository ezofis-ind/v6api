IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'PlaygroundApiKeyRoutes' AND schema_id = SCHEMA_ID(N'catalog'))
BEGIN
    CREATE TABLE catalog.PlaygroundApiKeyRoutes (
        ApiKey      NVARCHAR(128) NOT NULL CONSTRAINT PK_PlaygroundApiKeyRoutes PRIMARY KEY,
        TenantId    UNIQUEIDENTIFIER NOT NULL,
        KeyId       UNIQUEIDENTIFIER NOT NULL,
        Email       NVARCHAR(256) NOT NULL,
        IsActive    BIT NOT NULL CONSTRAINT DF_PlaygroundApiKeyRoutes_IsActive DEFAULT (1),
        CreatedAtUtc DATETIME2 NOT NULL
    );

    CREATE INDEX IX_PlaygroundApiKeyRoutes_TenantId ON catalog.PlaygroundApiKeyRoutes (TenantId);
    CREATE INDEX IX_PlaygroundApiKeyRoutes_Email ON catalog.PlaygroundApiKeyRoutes (Email);
END
GO
