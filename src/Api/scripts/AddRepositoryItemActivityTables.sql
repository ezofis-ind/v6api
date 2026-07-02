-- Optional repository item timeline/comments (run on tenant DB if tables are missing).
-- Safe to re-run. Upload/create repository do not require these tables.

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'repository')
    EXEC('CREATE SCHEMA repository');
GO

IF NOT EXISTS (SELECT * FROM sys.tables t INNER JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'ItemTimelineEvents' AND s.name = 'repository')
BEGIN
    CREATE TABLE repository.ItemTimelineEvents (
        Id              UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ItemTimelineEvents PRIMARY KEY DEFAULT NEWID(),
        TenantId        UNIQUEIDENTIFIER NOT NULL,
        RepositoryId    UNIQUEIDENTIFIER NOT NULL,
        ItemId          UNIQUEIDENTIFIER NOT NULL,
        EventType       NVARCHAR(64)  NOT NULL,
        Title           NVARCHAR(500) NOT NULL,
        Description     NVARCHAR(MAX) NULL,
        ActorType       NVARCHAR(64)  NULL,
        ActorName       NVARCHAR(256) NULL,
        ActorUserId     UNIQUEIDENTIFIER NULL,
        CreatedBy       UNIQUEIDENTIFIER NULL,
        CreatedAtUtc    DATETIME2(3) NOT NULL CONSTRAINT DF_ItemTimelineEvents_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        IsDeleted       BIT NOT NULL CONSTRAINT DF_ItemTimelineEvents_IsDeleted DEFAULT (0)
    );
    CREATE INDEX IX_ItemTimelineEvents_Item ON repository.ItemTimelineEvents (TenantId, RepositoryId, ItemId, IsDeleted, CreatedAtUtc);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables t INNER JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'ItemComments' AND s.name = 'repository')
BEGIN
    CREATE TABLE repository.ItemComments (
        Id              UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ItemComments PRIMARY KEY DEFAULT NEWID(),
        TenantId        UNIQUEIDENTIFIER NOT NULL,
        RepositoryId    UNIQUEIDENTIFIER NOT NULL,
        ItemId          UNIQUEIDENTIFIER NOT NULL,
        Body            NVARCHAR(MAX) NOT NULL,
        CreatedBy       UNIQUEIDENTIFIER NOT NULL,
        CreatedAtUtc    DATETIME2(3) NOT NULL CONSTRAINT DF_ItemComments_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        ModifiedAtUtc   DATETIME2(3) NULL,
        IsDeleted       BIT NOT NULL CONSTRAINT DF_ItemComments_IsDeleted DEFAULT (0)
    );
    CREATE INDEX IX_ItemComments_Item ON repository.ItemComments (TenantId, RepositoryId, ItemId, IsDeleted, CreatedAtUtc);
END
GO
