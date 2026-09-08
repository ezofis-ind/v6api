namespace SaaSApp.Reporting.Infrastructure.Persistence;

internal static class ReportSchemaSql
{
    public const string Ensure = """
        IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'reporting')
            EXEC(N'CREATE SCHEMA reporting');

        IF NOT EXISTS (
            SELECT 1 FROM sys.tables t
            INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
            WHERE s.name = N'reporting' AND t.name = N'ReportDefinitions')
        BEGIN
            CREATE TABLE reporting.ReportDefinitions (
                Id              UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ReportDefinitions PRIMARY KEY,
                TenantId        UNIQUEIDENTIFIER NOT NULL,
                Name            NVARCHAR(256) NOT NULL,
                Domain          NVARCHAR(256) NOT NULL,
                Description     NVARCHAR(MAX) NULL,
                SourceType      NVARCHAR(64) NULL,
                SourceFormId    NVARCHAR(64) NULL,
                WorkflowId      UNIQUEIDENTIFIER NULL,
                Status          NVARCHAR(32) NOT NULL,
                Visibility      NVARCHAR(64) NULL,
                OwnerUserId     UNIQUEIDENTIFIER NOT NULL,
                Scheduled       BIT NOT NULL CONSTRAINT DF_ReportDefinitions_Scheduled DEFAULT (0),
                Runs            INT NOT NULL CONSTRAINT DF_ReportDefinitions_Runs DEFAULT (0),
                ConfigJson      NVARCHAR(MAX) NOT NULL,
                HangfireJobId   NVARCHAR(128) NULL,
                CreatedAtUtc    DATETIME2(3) NOT NULL CONSTRAINT DF_ReportDefinitions_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
                ModifiedAtUtc   DATETIME2(3) NULL,
                CreatedBy       UNIQUEIDENTIFIER NOT NULL,
                ModifiedBy      UNIQUEIDENTIFIER NULL,
                IsDeleted       BIT NOT NULL CONSTRAINT DF_ReportDefinitions_IsDeleted DEFAULT (0)
            );
            CREATE INDEX IX_ReportDefinitions_Tenant
                ON reporting.ReportDefinitions (TenantId, IsDeleted, Status, Domain);
            CREATE INDEX IX_ReportDefinitions_Owner
                ON reporting.ReportDefinitions (TenantId, OwnerUserId, IsDeleted);
        END
        """;
}
