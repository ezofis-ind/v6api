-- =============================================
-- Create DMS Repository Items Table (per-repository)
-- Run when creating a new repository. Replace {suffix} with repo code (e.g. ezca_156).
-- Table structure: Year/InvoiceType/VendorName/FileName for folder archive path.
-- =============================================
-- Usage: Execute with @Suffix = 'ezca_156' (or your repository code)

DECLARE @Suffix NVARCHAR(64) = 'sample';  -- CHANGE THIS: e.g. 'ezca_156'
DECLARE @TableName NVARCHAR(128) = 'dms.[' + @Suffix + '_items]';

DECLARE @SQL NVARCHAR(MAX) = N'
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = ''' + @Suffix + '_items'' AND schema_id = SCHEMA_ID(''dms''))
BEGIN
    CREATE TABLE dms.[' + @Suffix + '_items] (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        TenantId UNIQUEIDENTIFIER NOT NULL,
        RepositoryId UNIQUEIDENTIFIER NOT NULL,
        [Year] SMALLINT NOT NULL,
        InvoiceType NVARCHAR(64) NOT NULL,
        VendorName NVARCHAR(256) NOT NULL,
        FileName NVARCHAR(512) NOT NULL,
        Status TINYINT NOT NULL DEFAULT 0,
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
        INDEX IX_' + @Suffix + '_Folder (RepositoryId, IsDeleted, [Year], InvoiceType, VendorName)
            INCLUDE (Id, FileName, Status, CreatedAt, WorkflowInstanceId),
        INDEX IX_' + @Suffix + '_Workflow (WorkflowInstanceId) WHERE WorkflowInstanceId IS NOT NULL
    );
    PRINT ''DMS: ' + @Suffix + '_items table created'';
END
';

EXEC sp_executesql @SQL;
GO
