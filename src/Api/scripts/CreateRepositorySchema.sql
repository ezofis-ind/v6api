-- =============================================
-- Repository module - base schema (tenant database)
-- STATIC repositories; GUID keys; per-repo Items_{suffix} created by API provisioner
-- =============================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'repository')
BEGIN
    EXEC('CREATE SCHEMA repository');
    PRINT 'repository schema created';
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables t INNER JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'StorageProviders' AND s.name = 'repository')
BEGIN
    CREATE TABLE repository.StorageProviders (
        Id              UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_StorageProviders PRIMARY KEY DEFAULT NEWID(),
        TenantId        UNIQUEIDENTIFIER NOT NULL,
        Code            NVARCHAR(32)  NOT NULL,
        Name            NVARCHAR(128) NOT NULL,
        ConfigJson      NVARCHAR(MAX) NULL,
        IsActive        BIT NOT NULL CONSTRAINT DF_StorageProviders_IsActive DEFAULT (1),
        CreatedAtUtc    DATETIME2(3) NOT NULL CONSTRAINT DF_StorageProviders_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        ModifiedAtUtc   DATETIME2(3) NULL,
        CreatedBy       UNIQUEIDENTIFIER NULL,
        ModifiedBy      UNIQUEIDENTIFIER NULL,
        IsDeleted       BIT NOT NULL CONSTRAINT DF_StorageProviders_IsDeleted DEFAULT (0),
        CONSTRAINT UQ_StorageProviders_TenantId_Code UNIQUE (TenantId, Code)
    );
    CREATE INDEX IX_StorageProviders_TenantId_IsDeleted ON repository.StorageProviders (TenantId, IsDeleted);
    PRINT 'repository.StorageProviders created';
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables t INNER JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'Repositories' AND s.name = 'repository')
BEGIN
    CREATE TABLE repository.Repositories (
        Id                  UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Repositories PRIMARY KEY DEFAULT NEWID(),
        TenantId            UNIQUEIDENTIFIER NOT NULL,
        Name                NVARCHAR(256) NOT NULL,
        Description         NVARCHAR(2000) NULL,
        FieldsType          NVARCHAR(32)  NOT NULL CONSTRAINT DF_Repositories_FieldsType DEFAULT ('STATIC'),
        StorageProviderId   UNIQUEIDENTIFIER NOT NULL,
        StorageDrive        NVARCHAR(500) NULL,
        ItemsTableName      NVARCHAR(128) NOT NULL,
        StageTableName      NVARCHAR(128) NOT NULL,
        IsDefaultRepository BIT NOT NULL CONSTRAINT DF_Repositories_IsDefaultRepository DEFAULT (1),
        CreatedAtUtc        DATETIME2(3) NOT NULL CONSTRAINT DF_Repositories_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        ModifiedAtUtc       DATETIME2(3) NULL,
        CreatedBy           UNIQUEIDENTIFIER NULL,
        ModifiedBy          UNIQUEIDENTIFIER NULL,
        IsDeleted           BIT NOT NULL CONSTRAINT DF_Repositories_IsDeleted DEFAULT (0),
        CONSTRAINT FK_Repositories_StorageProvider FOREIGN KEY (StorageProviderId) REFERENCES repository.StorageProviders (Id),
        CONSTRAINT CK_Repositories_FieldsType CHECK (FieldsType = 'STATIC')
    );
    CREATE INDEX IX_Repositories_TenantId_IsDeleted ON repository.Repositories (TenantId, IsDeleted);
    CREATE UNIQUE INDEX UX_Repositories_TenantId_Name ON repository.Repositories (TenantId, Name) WHERE IsDeleted = 0;
    PRINT 'repository.Repositories created';
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables t INNER JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'RepositoryFields' AND s.name = 'repository')
BEGIN
    CREATE TABLE repository.RepositoryFields (
        Id                          UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_RepositoryFields PRIMARY KEY DEFAULT NEWID(),
        RepositoryId                UNIQUEIDENTIFIER NOT NULL,
        Name                        NVARCHAR(200) NOT NULL,
        SqlColumnName               NVARCHAR(200) NOT NULL,
        DataType                    NVARCHAR(64)  NULL,
        Level                       INT NOT NULL CONSTRAINT DF_RepositoryFields_Level DEFAULT (0),
        IsMandatory                 BIT NOT NULL CONSTRAINT DF_RepositoryFields_IsMandatory DEFAULT (0),
        IncludeInFolderStructure    BIT NOT NULL CONSTRAINT DF_RepositoryFields_IncludeInFolderStructure DEFAULT (0),
        OptionsJson                 NVARCHAR(MAX) NULL,
        OrderId                     INT NULL,
        IsReadOnly                  BIT NOT NULL CONSTRAINT DF_RepositoryFields_IsReadOnly DEFAULT (0),
        CreatedAtUtc                  DATETIME2(3) NOT NULL CONSTRAINT DF_RepositoryFields_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        ModifiedAtUtc               DATETIME2(3) NULL,
        CreatedBy                   UNIQUEIDENTIFIER NULL,
        ModifiedBy                  UNIQUEIDENTIFIER NULL,
        IsDeleted                   BIT NOT NULL CONSTRAINT DF_RepositoryFields_IsDeleted DEFAULT (0),
        CONSTRAINT FK_RepositoryFields_Repository FOREIGN KEY (RepositoryId) REFERENCES repository.Repositories (Id) ON DELETE CASCADE
    );
    CREATE INDEX IX_RepositoryFields_RepositoryId_IsDeleted ON repository.RepositoryFields (RepositoryId, IsDeleted);
    PRINT 'repository.RepositoryFields created';
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables t INNER JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'Folders' AND s.name = 'repository')
BEGIN
    CREATE TABLE repository.Folders (
        Id              UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Folders PRIMARY KEY DEFAULT NEWID(),
        TenantId        UNIQUEIDENTIFIER NOT NULL,
        RepositoryId    UNIQUEIDENTIFIER NOT NULL,
        Name            NVARCHAR(256) NOT NULL,
        ParentId        UNIQUEIDENTIFIER NULL,
        LevelId         INT NOT NULL CONSTRAINT DF_Folders_LevelId DEFAULT (0),
        PathId          NVARCHAR(512) NULL,
        CreatedAtUtc    DATETIME2(3) NOT NULL CONSTRAINT DF_Folders_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        ModifiedAtUtc   DATETIME2(3) NULL,
        CreatedBy       UNIQUEIDENTIFIER NULL,
        ModifiedBy      UNIQUEIDENTIFIER NULL,
        IsDeleted       BIT NOT NULL CONSTRAINT DF_Folders_IsDeleted DEFAULT (0),
        CONSTRAINT FK_Folders_Repository FOREIGN KEY (RepositoryId) REFERENCES repository.Repositories (Id) ON DELETE CASCADE
    );
    CREATE INDEX IX_Folders_RepositoryId_ParentId_IsDeleted ON repository.Folders (RepositoryId, ParentId, IsDeleted);
    PRINT 'repository.Folders created';
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables t INNER JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'SavedViews' AND s.name = 'repository')
BEGIN
    CREATE TABLE repository.SavedViews (
        Id              UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SavedViews PRIMARY KEY DEFAULT NEWID(),
        TenantId        UNIQUEIDENTIFIER NOT NULL,
        RepositoryId    UNIQUEIDENTIFIER NOT NULL,
        UserId          UNIQUEIDENTIFIER NOT NULL,
        Name            NVARCHAR(256) NOT NULL,
        FilterJson      NVARCHAR(MAX) NOT NULL,
        SortJson        NVARCHAR(MAX) NULL,
        CreatedAtUtc    DATETIME2(3) NOT NULL CONSTRAINT DF_SavedViews_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        ModifiedAtUtc   DATETIME2(3) NULL,
        IsDeleted       BIT NOT NULL CONSTRAINT DF_SavedViews_IsDeleted DEFAULT (0),
        CONSTRAINT FK_SavedViews_Repository FOREIGN KEY (RepositoryId) REFERENCES repository.Repositories (Id) ON DELETE CASCADE
    );
    CREATE INDEX IX_SavedViews_RepositoryId_UserId ON repository.SavedViews (RepositoryId, UserId, IsDeleted);
    PRINT 'repository.SavedViews created';
END
GO

-- Optional item activity (timeline/comments). Not required for repository create or file upload.
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
    PRINT 'repository.ItemTimelineEvents created';
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
    PRINT 'repository.ItemComments created';
END
GO

PRINT 'Repository base schema complete.';
GO

IF COL_LENGTH('repository.Repositories', 'IsDefaultRepository') IS NULL
BEGIN
    ALTER TABLE repository.Repositories
        ADD IsDefaultRepository BIT NOT NULL CONSTRAINT DF_Repositories_IsDefaultRepository DEFAULT (1);
    PRINT 'repository.Repositories.IsDefaultRepository added';
END
GO
