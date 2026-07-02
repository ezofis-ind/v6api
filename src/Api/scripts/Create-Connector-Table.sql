-- dbo.connector (SaaS API — GUID primary key). Run on tenant database if table does not exist.
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'connector' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.connector(
        id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        name NVARCHAR(500) NULL,
        connectorType NVARCHAR(500) NULL,
        credentialJson NVARCHAR(MAX) NULL,
        dynamicCredentialJson NVARCHAR(MAX) NULL,
        responseStatus NVARCHAR(50) NULL,
        responseStatusCode NVARCHAR(50) NULL,
        responseBody NVARCHAR(MAX) NULL,
        createdAt NVARCHAR(50) NULL,
        modifiedAt NVARCHAR(50) NULL,
        createdBy NVARCHAR(50) NOT NULL DEFAULT('0'),
        modifiedBy NVARCHAR(50) NOT NULL DEFAULT('0'),
        isDeleted BIT NOT NULL DEFAULT(0),
        Preference BIT NOT NULL DEFAULT(0)
    );
    CREATE INDEX IX_connector_isDeleted ON dbo.connector(isDeleted);
END
