-- Catalog DB: cross-tenant repository item share grants
IF NOT EXISTS (
    SELECT 1 FROM sys.tables t
    INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
    WHERE s.name = N'catalog' AND t.name = N'RepositoryItemShares')
BEGIN
    CREATE TABLE catalog.RepositoryItemShares (
        Id                  UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_RepositoryItemShares PRIMARY KEY DEFAULT NEWID(),
        ShareToken          NVARCHAR(128)    NOT NULL,
        SourceTenantId      UNIQUEIDENTIFIER NOT NULL,
        SourceRepositoryId  UNIQUEIDENTIFIER NOT NULL,
        SourceItemId        UNIQUEIDENTIFIER NOT NULL,
        SharedByUserId      UNIQUEIDENTIFIER NOT NULL,
        RecipientEmail      NVARCHAR(256)    NOT NULL,
        Message             NVARCHAR(2000)   NULL,
        Status              NVARCHAR(32)     NOT NULL CONSTRAINT DF_RepositoryItemShares_Status DEFAULT N'Active',
        ExpiresAtUtc        DATETIME2        NOT NULL,
        CreatedAtUtc          DATETIME2        NOT NULL CONSTRAINT DF_RepositoryItemShares_CreatedAt DEFAULT SYSUTCDATETIME(),
        LastAccessedAtUtc   DATETIME2        NULL
    );

    CREATE UNIQUE INDEX IX_RepositoryItemShares_ShareToken
        ON catalog.RepositoryItemShares (ShareToken);

    CREATE INDEX IX_RepositoryItemShares_Recipient_Status
        ON catalog.RepositoryItemShares (RecipientEmail, Status);

    CREATE INDEX IX_RepositoryItemShares_Source
        ON catalog.RepositoryItemShares (SourceTenantId, SourceRepositoryId, SourceItemId);
END
GO
