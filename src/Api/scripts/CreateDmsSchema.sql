-- =============================================
-- DMS (Document Management System) Schema
-- Folder structure: Year/InvoiceType/VendorName/FileName
-- Run on tenant database. Created automatically on tenant signup.
-- =============================================

-- Create dms schema
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'dms')
BEGIN
    EXEC('CREATE SCHEMA dms');
END
GO

-- =============================================
-- Repository (document repository metadata)
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Repository' AND schema_id = SCHEMA_ID('dms'))
BEGIN
    CREATE TABLE dms.Repository (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        TenantId UNIQUEIDENTIFIER NOT NULL,
        Code NVARCHAR(32) NOT NULL,
        Name NVARCHAR(256) NOT NULL,
        Description NVARCHAR(2000) NULL,
        ItemsTableName NVARCHAR(128) NOT NULL,
        CreatedAtUtc DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        ModifiedAtUtc DATETIME2(3) NULL,
        UNIQUE (TenantId, Code),
        INDEX IX_Repository_TenantId (TenantId)
    );
    PRINT 'DMS: Repository table created';
END
GO

-- =============================================
-- RepositoryFolderConfig (folder structure definition per repo)
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'RepositoryFolderConfig' AND schema_id = SCHEMA_ID('dms'))
BEGIN
    CREATE TABLE dms.RepositoryFolderConfig (
        RepositoryId UNIQUEIDENTIFIER NOT NULL,
        LevelOrder TINYINT NOT NULL,
        FieldName NVARCHAR(64) NOT NULL,
        DisplayName NVARCHAR(128) NOT NULL,
        PRIMARY KEY (RepositoryId, LevelOrder)
    );
    PRINT 'DMS: RepositoryFolderConfig table created';
END
GO

-- =============================================
-- DocumentWorkflowLink (shared - links documents to workflow instances)
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DocumentWorkflowLink' AND schema_id = SCHEMA_ID('dms'))
BEGIN
    CREATE TABLE dms.DocumentWorkflowLink (
        DocumentId UNIQUEIDENTIFIER NOT NULL,
        RepositoryId UNIQUEIDENTIFIER NOT NULL,
        WorkflowInstanceId UNIQUEIDENTIFIER NOT NULL,
        WorkflowId UNIQUEIDENTIFIER NOT NULL,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        LinkedAtUtc DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        PRIMARY KEY (DocumentId, RepositoryId),
        INDEX IX_DocumentWorkflowLink_WorkflowInstance (WorkflowInstanceId),
        INDEX IX_DocumentWorkflowLink_Tenant_Workflow (TenantId, WorkflowId)
    );
    PRINT 'DMS: DocumentWorkflowLink table created';
END
GO

-- =============================================
-- StagingItems: Temp indexing (upload + manual index before Export)
-- Holds: uploaded file ref + index values (Year, InvoiceType, VendorName, FileName)
-- On Export: move/copy to sample_items (or repo items table)
-- Status: 0=Draft (indexed, not exported), 1=Exported
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'StagingItems' AND schema_id = SCHEMA_ID('dms'))
BEGIN
    CREATE TABLE dms.StagingItems (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        TenantId UNIQUEIDENTIFIER NOT NULL,
        RepositoryId UNIQUEIDENTIFIER NOT NULL,
        [Year] SMALLINT NOT NULL,
        InvoiceType NVARCHAR(64) NOT NULL,
        VendorName NVARCHAR(256) NOT NULL,
        FileName NVARCHAR(512) NOT NULL,
        FilePath NVARCHAR(1024) NULL,
        StoragePath NVARCHAR(1024) NULL,
        Status TINYINT NOT NULL DEFAULT 0,
        CreatedAt DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedBy UNIQUEIDENTIFIER NOT NULL,
        UpdatedAt DATETIME2(3) NULL,
        UpdatedBy UNIQUEIDENTIFIER NULL,
        ExportedAt DATETIME2(3) NULL,
        ExportedToItemId UNIQUEIDENTIFIER NULL,
        INDEX IX_StagingItems_Repository_Status (RepositoryId, Status),
        INDEX IX_StagingItems_CreatedBy (CreatedBy),
        INDEX IX_StagingItems_CreatedAt (CreatedAt DESC)
    );
    PRINT 'DMS: StagingItems table created (temp indexing before export)';
END
GO

-- =============================================
-- Sample repository items table (for testing)
-- Create with suffix 'sample' - table: dms.sample_items
-- Status: 0=Draft, 1=Exported, 2=Archived
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'sample_items' AND schema_id = SCHEMA_ID('dms'))
BEGIN
    CREATE TABLE dms.sample_items (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        TenantId UNIQUEIDENTIFIER NOT NULL,
        RepositoryId UNIQUEIDENTIFIER NOT NULL,
        [Year] SMALLINT NOT NULL,
        InvoiceType NVARCHAR(64) NOT NULL,
        VendorName NVARCHAR(256) NOT NULL,
        FileName NVARCHAR(512) NOT NULL,
        Status TINYINT NOT NULL DEFAULT 0,  /* 0=Draft, 1=Exported, 2=Archived */
        SignStatus TINYINT NOT NULL DEFAULT 0,
        CreatedAt DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAt DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedBy UNIQUEIDENTIFIER NOT NULL,
        UpdatedBy UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL DEFAULT 0,
        DeletedAt DATETIME2(3) NULL,
        Version INT NOT NULL DEFAULT 1,
        WorkflowInstanceId UNIQUEIDENTIFIER NULL,
        ReportNo NVARCHAR(128) NULL,
        ReferenceNo NVARCHAR(64) NULL,
        INDEX IX_sample_items_Folder (RepositoryId, IsDeleted, [Year], InvoiceType, VendorName)
            INCLUDE (Id, FileName, Status, CreatedAt, WorkflowInstanceId),
        INDEX IX_sample_items_Workflow (WorkflowInstanceId) WHERE WorkflowInstanceId IS NOT NULL
    );
    PRINT 'DMS: sample_items table created';
END
GO

PRINT 'DMS schema setup complete.';
GO
