using System.Collections.Concurrent;
using Microsoft.Data.SqlClient;
using SaaSApp.Workflow.Application.Contracts;

namespace SaaSApp.Workflow.Infrastructure.Services;

/// <summary>
/// Creates dynamic tables for each workflow.
/// When a workflow is published, creates dedicated tables: Comments_X, Attachments_X, etc.
/// </summary>
public sealed class WorkflowTableCreator : IWorkflowTableCreator
{
    private static readonly ConcurrentDictionary<string, byte> LegacyTransactionSchemaEnsured = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, byte> LegacyMailboxSchemaEnsured = new(StringComparer.OrdinalIgnoreCase);

    public async Task<bool> WorkflowCoreTablesExistAsync(
        Guid workflowId,
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        var suffix = workflowId.ToString("N")[..8];
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT CASE WHEN EXISTS (
                SELECT 1
                FROM sys.tables t
                INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                WHERE s.name = N'workflow' AND t.name = @TableName
            ) THEN 1 ELSE 0 END;
            """;

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@TableName", $"WorkflowInstances_{suffix}");
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken)) == 1;
    }

    /// <summary>
    /// Fast path for workflow start: skip full DDL when tables already exist (created at publish).
    /// </summary>
    public async Task EnsureWorkflowTablesForStartAsync(
        Guid workflowId,
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        if (await WorkflowCoreTablesExistAsync(workflowId, connectionString, cancellationToken))
        {
            await EnsureLegacyTransactionTableAsync(workflowId, connectionString, cancellationToken);
            await EnsureLegacyMailboxTablesAsync(workflowId, connectionString, cancellationToken);
            await EnsureAgentDataValidationTableAsync(workflowId, connectionString, cancellationToken);
            return;
        }

        await CreateWorkflowTablesAsync(workflowId, connectionString, cancellationToken);
    }

    public async Task CreateWorkflowTablesAsync(Guid workflowId, string connectionString, CancellationToken cancellationToken = default)
    {
        var workflowIdStr = workflowId.ToString("N"); // Remove hyphens for table names
        var workflowIdDashed = workflowId.ToString("D");
        var suffix = workflowIdStr.Substring(0, 8); // Use first 8 chars as suffix

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using (var schemaCmd = new SqlCommand("""
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'workflow')
    EXEC(N'CREATE SCHEMA workflow');
""", connection))
        {
            schemaCmd.CommandTimeout = 120;
            await schemaCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        var tables = new[]
        {
            GenerateWorkflowInstancesTableScript(suffix),
            GenerateWorkflowStepInstancesTableScript(suffix),
            GenerateWorkflowInstanceSlasTableScript(suffix),
            GenerateWorkflowInstanceUserStateTableScript(suffix),
            GenerateLegacyTransactionTableScript(suffix),
            GenerateProcessFormTableScript(suffix),
            GenerateProcessAddonTableScript(suffix),
            GenerateCommentsTableScript(suffix),
            GenerateAttachmentsTableScript(suffix),
            GenerateFormsTableScript(suffix),
            GenerateTasksTableScript(suffix),
            GenerateSignaturesTableScript(suffix),
            GenerateDocumentsTableScript(suffix),
            GenerateEmailsTableScript(suffix),
            GenerateAiValidationsTableScript(suffix),
            GenerateAgentDataValidationTableScript(suffix),
            GeneratePdfAnnotationsTableScript(suffix)
        };

        foreach (var script in tables)
        {
            await using var command = new SqlCommand(script, connection);
            command.CommandTimeout = 120;
            try
            {
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException(
                    $"Failed creating workflow tables for suffix '{suffix}' (workflow {workflowIdDashed}): {ex.Message}",
                    ex);
            }
        }

        await EnsureLegacyTransactionTableAsync(connection, suffix, cancellationToken);
        await EnsureLegacyMailboxTablesAsync(connection, suffix, cancellationToken);

        LegacyTransactionSchemaEnsured.TryAdd(suffix, 0);
        LegacyMailboxSchemaEnsured.TryAdd(suffix, 0);
    }

    /// <summary>Creates or upgrades workflow.transaction_{suffix} (TransactionGuid column + index).</summary>
    public async Task EnsureLegacyTransactionTableAsync(
        Guid workflowId,
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        var suffix = workflowId.ToString("N")[..8];
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await EnsureLegacyTransactionTableAsync(connection, suffix, cancellationToken);
    }

    public async Task EnsureLegacyMailboxTablesAsync(
        Guid workflowId,
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        var suffix = workflowId.ToString("N")[..8];
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await EnsureLegacyMailboxTablesAsync(connection, suffix, cancellationToken);
    }

    public async Task EnsureAgentDataValidationTableAsync(
        Guid workflowId,
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        var suffix = workflowId.ToString("N")[..8];
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var cmd = new SqlCommand(GenerateAgentDataValidationTableScript(suffix), connection)
        {
            CommandTimeout = 120
        };
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureLegacyTransactionTableAsync(
        SqlConnection connection,
        string suffix,
        CancellationToken cancellationToken)
    {
        if (LegacyTransactionSchemaEnsured.ContainsKey(suffix))
            return;

        await using (var cmd = new SqlCommand(GenerateLegacyTransactionTableScript(suffix), connection))
        {
            cmd.CommandTimeout = 120;
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        var tableFullName = $"workflow.transaction_{suffix}";
        var alterSql = $@"
IF COL_LENGTH('{tableFullName}', 'TransactionGuid') IS NULL
BEGIN
    ALTER TABLE workflow.[transaction_{suffix}] ADD TransactionGuid UNIQUEIDENTIFIER NULL;
END";
        await using (var alterCmd = new SqlCommand(alterSql, connection))
        {
            alterCmd.CommandTimeout = 120;
            await alterCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        var idx = suffix.Replace("-", "_");
        var indexSql = $@"
IF COL_LENGTH('{tableFullName}', 'TransactionGuid') IS NOT NULL
  AND NOT EXISTS (
    SELECT 1 FROM sys.indexes i
    INNER JOIN sys.tables t ON i.object_id = t.object_id
    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE s.name = N'workflow' AND t.name = N'transaction_{suffix}'
      AND i.name = N'IX_transaction_{idx}_TransactionGuid')
BEGIN
    EXEC(N'CREATE UNIQUE NONCLUSTERED INDEX [IX_transaction_{idx}_TransactionGuid]
        ON workflow.[transaction_{suffix}](TransactionGuid)
        WHERE TransactionGuid IS NOT NULL');
END";
        await using (var indexCmd = new SqlCommand(indexSql, connection))
        {
            indexCmd.CommandTimeout = 120;
            await indexCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await MigrateTransactionWorkflowInstanceIdAsync(connection, suffix, cancellationToken);
        LegacyTransactionSchemaEnsured.TryAdd(suffix, 0);
    }

    private static async Task MigrateTransactionWorkflowInstanceIdAsync(
        SqlConnection connection,
        string suffix,
        CancellationToken cancellationToken)
    {
        var tableFullName = $"workflow.transaction_{suffix}";
        var idx = suffix.Replace("-", "_");
        var migrateSql = $@"
IF COL_LENGTH('{tableFullName}', 'WorkflowInstanceId') IS NULL
BEGIN
    ALTER TABLE workflow.[transaction_{suffix}] ADD WorkflowInstanceId UNIQUEIDENTIFIER NULL;
END";

        await using (var cmd = new SqlCommand(migrateSql, connection) { CommandTimeout = 120 })
            await cmd.ExecuteNonQueryAsync(cancellationToken);

        var finalizeSql = $@"
IF COL_LENGTH('{tableFullName}', 'ProcessId') IS NOT NULL
BEGIN
    IF EXISTS (
        SELECT 1 FROM sys.indexes i
        INNER JOIN sys.tables t ON i.object_id = t.object_id
        INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
        WHERE s.name = N'workflow' AND t.name = N'transaction_{suffix}'
          AND i.name = N'IX_transaction_{idx}_ProcessId_IsDeleted')
    BEGIN
        DROP INDEX [IX_transaction_{idx}_ProcessId_IsDeleted] ON workflow.[transaction_{suffix}];
    END
END
IF COL_LENGTH('{tableFullName}', 'ProcessId') IS NOT NULL
   AND COL_LENGTH('{tableFullName}', 'WorkflowInstanceId') IS NOT NULL
BEGIN
    ALTER TABLE workflow.[transaction_{suffix}] DROP COLUMN ProcessId;
END";

        await using (var finalizeCmd = new SqlCommand(finalizeSql, connection) { CommandTimeout = 120 })
            await finalizeCmd.ExecuteNonQueryAsync(cancellationToken);

        var indexSql = $@"
IF COL_LENGTH('{tableFullName}', 'WorkflowInstanceId') IS NOT NULL
  AND NOT EXISTS (
    SELECT 1 FROM sys.indexes i
    INNER JOIN sys.tables t ON i.object_id = t.object_id
    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE s.name = N'workflow' AND t.name = N'transaction_{suffix}'
      AND i.name = N'IX_transaction_{idx}_WorkflowInstanceId_IsDeleted')
BEGIN
    EXEC(N'CREATE NONCLUSTERED INDEX [IX_transaction_{idx}_WorkflowInstanceId_IsDeleted]
        ON workflow.[transaction_{suffix}](WorkflowInstanceId, IsDeleted)');
END";
        await using var indexCmd = new SqlCommand(indexSql, connection) { CommandTimeout = 120 };
        await indexCmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DropWorkflowTablesAsync(Guid workflowId, string connectionString, CancellationToken cancellationToken = default)
    {
        var workflowIdStr = workflowId.ToString("N");
        var suffix = workflowIdStr.Substring(0, 8);

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        // Remove lookup rows for this workflow's instances before dropping tables
        var deleteLookup = "DELETE FROM workflow.WorkflowInstanceLookup WHERE WorkflowId = @WorkflowId";
        await using (var cmd = new SqlCommand(deleteLookup, connection))
        {
            cmd.Parameters.AddWithValue("@WorkflowId", workflowId);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        var tableNames = new[]
        {
            $"WorkflowInstanceSlas_{suffix}",
            $"WorkflowStepInstances_{suffix}",
            $"WorkflowInstances_{suffix}",
            $"WorkflowInstanceUserState_{suffix}",
            $"transaction_{suffix}",
            $"processForm_{suffix}",
            $"processAddon_{suffix}",
            $"Inbox_{suffix}",
            $"Sent_{suffix}",
            $"Completed_{suffix}",
            $"Inbox_{workflowIdStr}",
            $"Sent_{workflowIdStr}",
            $"Completed_{workflowIdStr}",
            $"WorkflowComments_{suffix}",
            $"WorkflowAttachments_{suffix}",
            $"WorkflowForms_{suffix}",
            $"WorkflowTasks_{suffix}",
            $"WorkflowSignatures_{suffix}",
            $"WorkflowDocuments_{suffix}",
            $"WorkflowEmails_{suffix}",
            $"WorkflowAiValidations_{suffix}",
            $"agentDataValidation_{suffix}",
            $"WorkflowPdfAnnotations_{suffix}"
        };

        foreach (var tableName in tableNames)
        {
            var script = $@"
                IF EXISTS (SELECT * FROM sys.tables WHERE name = '{tableName}' AND schema_id = SCHEMA_ID('workflow'))
                BEGIN
                    DROP TABLE workflow.[{tableName}];
                END";

            await using var command = new SqlCommand(script, connection);
            command.CommandTimeout = 120;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task EnsureLegacyMailboxTablesAsync(
        SqlConnection connection,
        string workflowKey,
        CancellationToken cancellationToken)
    {
        if (!LegacyMailboxSchemaEnsured.ContainsKey(workflowKey))
        {
            var inbox = GenerateLegacyMailboxTableScript("Inbox", workflowKey);
            var sent = GenerateLegacyMailboxTableScript("Sent", workflowKey);
            var completed = GenerateLegacyMailboxTableScript("Completed", workflowKey);

            foreach (var script in new[] { inbox, sent, completed })
            {
                await using var cmd = new SqlCommand(script, connection);
                cmd.CommandTimeout = 120;
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            await EnsureLegacyMailboxIndexesAsync(connection, workflowKey, cancellationToken);
            await EnsureLegacyTransactionMailboxIndexesAsync(connection, workflowKey, cancellationToken);
            LegacyMailboxSchemaEnsured.TryAdd(workflowKey, 0);
        }

        // Always run idempotent column migrates (e.g. action) so existing DBs pick up new columns.
        await MigrateLegacyMailboxInstanceColumnsAsync(connection, workflowKey, cancellationToken);
    }

    private static async Task EnsureLegacyTransactionMailboxIndexesAsync(
        SqlConnection connection,
        string workflowKey,
        CancellationToken cancellationToken)
    {
        var table = $"transaction_{workflowKey}";
        var idx = $"IX_{table}_Instance_Status";
        var sql = $@"
IF EXISTS (SELECT 1 FROM sys.tables WHERE name = N'{table}' AND schema_id = SCHEMA_ID(N'workflow'))
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'{idx}' AND object_id = OBJECT_ID(N'workflow.[{table}]'))
    CREATE NONCLUSTERED INDEX [{idx}]
    ON workflow.[{table}] (WorkflowInstanceId, IsDeleted, ActionStatus)
    INCLUDE (ActivityUserId, CreatedBy, StageType, ActivityGroupId);";
        await using var cmd = new SqlCommand(sql, connection) { CommandTimeout = 120 };
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureLegacyMailboxIndexesAsync(
        SqlConnection connection,
        string workflowKey,
        CancellationToken cancellationToken)
    {
        foreach (var prefix in new[] { "Inbox", "Sent", "Completed" })
        {
            var table = $"{prefix}_{workflowKey}";
            var idxInstance = $"IX_{table}_Instance_Created";
            var idxUser = $"IX_{table}_User_Created";
            var sql = $@"
IF EXISTS (SELECT 1 FROM sys.tables WHERE name = N'{table}' AND schema_id = SCHEMA_ID(N'workflow'))
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'{idxInstance}' AND object_id = OBJECT_ID(N'workflow.[{table}]'))
        CREATE NONCLUSTERED INDEX [{idxInstance}]
        ON workflow.[{table}] (workflowInstanceId, transaction_createdAt DESC, id DESC)
        INCLUDE (userId, transaction_createdBy, transactionId, name, referenceNumber, stage, review);
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'{idxUser}' AND object_id = OBJECT_ID(N'workflow.[{table}]'))
        CREATE NONCLUSTERED INDEX [{idxUser}]
        ON workflow.[{table}] (userId, transaction_createdAt DESC, id DESC)
        INCLUDE (transaction_createdBy, workflowInstanceId, transactionId, name, referenceNumber);
END";
            await using var cmd = new SqlCommand(sql, connection) { CommandTimeout = 120 };
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task MigrateLegacyMailboxInstanceColumnsAsync(
        SqlConnection connection,
        string workflowKey,
        CancellationToken cancellationToken)
    {
        foreach (var prefix in new[] { "Inbox", "Sent", "Completed" })
        {
            var tableFullName = $"workflow.{prefix}_{workflowKey}";
            var sql = $@"
IF EXISTS (SELECT 1 FROM sys.tables WHERE name = '{prefix}_{workflowKey}' AND schema_id = SCHEMA_ID('workflow'))
BEGIN
    IF COL_LENGTH('{tableFullName}', 'workflowInstanceId') IS NULL
        ALTER TABLE workflow.[{prefix}_{workflowKey}] ADD workflowInstanceId nvarchar(255) NULL;
    IF COL_LENGTH('{tableFullName}', 'referenceNumber') IS NULL
        ALTER TABLE workflow.[{prefix}_{workflowKey}] ADD referenceNumber nvarchar(128) NULL;
    IF COL_LENGTH('{tableFullName}', 'createdAtUtc') IS NULL
        ALTER TABLE workflow.[{prefix}_{workflowKey}] ADD createdAtUtc datetime2 NULL;
    IF COL_LENGTH('{tableFullName}', 'startedAtUtc') IS NULL
        ALTER TABLE workflow.[{prefix}_{workflowKey}] ADD startedAtUtc datetime2 NULL;
    IF COL_LENGTH('{tableFullName}', 'completedAtUtc') IS NULL
        ALTER TABLE workflow.[{prefix}_{workflowKey}] ADD completedAtUtc datetime2 NULL;
    IF COL_LENGTH('{tableFullName}', 'context') IS NULL
        ALTER TABLE workflow.[{prefix}_{workflowKey}] ADD context nvarchar(4000) NULL;
    IF COL_LENGTH('{tableFullName}', 'repositoryId') IS NULL
        ALTER TABLE workflow.[{prefix}_{workflowKey}] ADD repositoryId nvarchar(255) NULL;
    IF COL_LENGTH('{tableFullName}', 'itemId') IS NULL
        ALTER TABLE workflow.[{prefix}_{workflowKey}] ADD itemId nvarchar(255) NULL;
    IF COL_LENGTH('{tableFullName}', 'formId') IS NULL
        ALTER TABLE workflow.[{prefix}_{workflowKey}] ADD formId nvarchar(255) NULL;
    IF COL_LENGTH('{tableFullName}', 'formEntryId') IS NULL
        ALTER TABLE workflow.[{prefix}_{workflowKey}] ADD formEntryId nvarchar(255) NULL;
    IF COL_LENGTH('{tableFullName}', 'formData') IS NULL
        ALTER TABLE workflow.[{prefix}_{workflowKey}] ADD formData nvarchar(max) NULL;
    ELSE IF EXISTS (
        SELECT 1
        FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = N'workflow'
          AND TABLE_NAME = N'{prefix}_{workflowKey}'
          AND COLUMN_NAME = N'formData'
          AND CHARACTER_MAXIMUM_LENGTH <> -1
    )
        ALTER TABLE workflow.[{prefix}_{workflowKey}] ALTER COLUMN formData nvarchar(max) NULL;
    IF COL_LENGTH('{tableFullName}', 'action') IS NULL
        ALTER TABLE workflow.[{prefix}_{workflowKey}] ADD [action] int NOT NULL
            CONSTRAINT [DF_{prefix}_{workflowKey}_action] DEFAULT (1);
END";
            await using var cmd = new SqlCommand(sql, connection) { CommandTimeout = 120 };
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static string GenerateLegacyMailboxTableScript(string prefix, string workflowKey)
    {
        // Based on provided schema; kept identical across Inbox/Sent/Completed.
        return $@"
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = '{prefix}_{workflowKey}' AND schema_id = SCHEMA_ID('workflow'))
BEGIN
    CREATE TABLE workflow.[{prefix}_{workflowKey}] (
        id int IDENTITY(1,1) NOT NULL,
        userId nvarchar(100) NULL,
        groupId int NULL,
        workflowId nvarchar(255) NULL,
        name nvarchar(255) NULL,
        workflowInstanceId nvarchar(255) NULL,
        referenceNumber nvarchar(128) NULL,
        createdAtUtc datetime2 NULL,
        startedAtUtc datetime2 NULL,
        completedAtUtc datetime2 NULL,
        context nvarchar(4000) NULL,
        transactionId nvarchar(255) NULL,
        activityId nvarchar(255) NULL,
        ruleId nvarchar(255) NULL,
        stageType nvarchar(255) NULL,
        stage nvarchar(255) NULL,
        review nvarchar(255) NULL,
        transaction_createdAt datetime NULL,
        transaction_createdBy nvarchar(255) NULL,
        transaction_createdByEmail nvarchar(255) NULL,
        repositoryId nvarchar(255) NULL,
        itemId nvarchar(255) NULL,
        formId nvarchar(255) NULL,
        formEntryId nvarchar(255) NULL,
        mlPrediction nvarchar(255) NULL,
        transaction_modifiedAt datetime NULL,
        transaction_modifiedBy nvarchar(255) NULL,
        mlCondition nvarchar(255) NULL,
        userType nvarchar(255) NULL,
        jiraIssueJson nvarchar(max) NULL,
        createdByName nvarchar(255) NULL,
        jiraIssueInfo nvarchar(max) NULL,
        lastActionStageType varchar(50) NULL,
        lastActionStageName varchar(50) NULL,
        lastAction varchar(50) NULL,
        formData nvarchar(max) NULL,
        commentsCount int NULL,
        attachmentCount int NULL,
        itemData nvarchar(max) NULL,
        activityUserEmail nvarchar(max) NULL,
        activityGroupName nvarchar(max) NULL,
        [action] int NOT NULL CONSTRAINT [DF_{prefix}_{workflowKey}_action] DEFAULT (1),
        PRIMARY KEY CLUSTERED (id ASC)
    ) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
END";
    }

    private static string GenerateWorkflowInstancesTableScript(string suffix)
    {
        return $@"
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'WorkflowInstances_{suffix}' AND schema_id = SCHEMA_ID('workflow'))
BEGIN
    CREATE TABLE workflow.WorkflowInstances_{suffix} (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        TenantId UNIQUEIDENTIFIER NOT NULL,
        WorkflowId UNIQUEIDENTIFIER NOT NULL,
        WorkflowName NVARCHAR(256) NOT NULL,
        WorkflowVersion INT NOT NULL,
        Status INT NOT NULL,
        CurrentStepInstanceId UNIQUEIDENTIFIER NULL,
        CreatedAtUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        StartedAtUtc DATETIME2 NULL,
        CompletedAtUtc DATETIME2 NULL,
        StartedBy UNIQUEIDENTIFIER NOT NULL,
        Context NVARCHAR(4000) NULL,
        ErrorMessage NVARCHAR(2000) NULL,
        ReferenceNumber NVARCHAR(128) NULL,
        CustomerName NVARCHAR(256) NULL,
        CustomerEmail NVARCHAR(256) NULL,
        CustomerPhone NVARCHAR(64) NULL,
        Department NVARCHAR(128) NULL,
        Category NVARCHAR(128) NULL,
        Priority INT NOT NULL DEFAULT 1,
        Tags NVARCHAR(1000) NULL,
        CustomFieldsJson NVARCHAR(4000) NULL,
        AssignedToUserId UNIQUEIDENTIFIER NULL,
        AssignedToGroupId UNIQUEIDENTIFIER NULL,
        LastActivityAtUtc DATETIME2 NULL,
        ViewCount INT NOT NULL DEFAULT 0,
        IsArchived BIT NOT NULL DEFAULT 0,
        ArchivedAtUtc DATETIME2 NULL,
        SourceType NVARCHAR(64) NULL,
        SourceId NVARCHAR(256) NULL,
        LastViewedAtUtc DATETIME2 NULL,
        LastViewedBy UNIQUEIDENTIFIER NULL
    );
END
{EnsureNonClusteredIndex($"workflow.WorkflowInstances_{suffix}", $"IX_WorkflowInstances_{suffix}_TenantId_WorkflowId", "TenantId, WorkflowId")}
{EnsureNonClusteredIndex($"workflow.WorkflowInstances_{suffix}", $"IX_WorkflowInstances_{suffix}_TenantId_Status_IsArchived", "TenantId, Status, IsArchived")}
{EnsureNonClusteredIndex($"workflow.WorkflowInstances_{suffix}", $"IX_WorkflowInstances_{suffix}_ReferenceNumber", "ReferenceNumber")}";
    }

    private static string GenerateWorkflowStepInstancesTableScript(string suffix)
    {
        return $@"
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'WorkflowStepInstances_{suffix}' AND schema_id = SCHEMA_ID('workflow'))
BEGIN
    CREATE TABLE workflow.WorkflowStepInstances_{suffix} (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        WorkflowInstanceId UNIQUEIDENTIFIER NOT NULL,
        WorkflowStepId UNIQUEIDENTIFIER NOT NULL,
        StepName NVARCHAR(256) NOT NULL,
        StepType INT NOT NULL,
        [Order] INT NOT NULL,
        Status INT NOT NULL,
        AssignedToUserId UNIQUEIDENTIFIER NULL,
        AssignedToRole NVARCHAR(64) NULL,
        CreatedAtUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        StartedAtUtc DATETIME2 NULL,
        CompletedAtUtc DATETIME2 NULL,
        CompletedBy UNIQUEIDENTIFIER NULL,
        Result NVARCHAR(4000) NULL,
        ErrorMessage NVARCHAR(2000) NULL,
        ActivityId NVARCHAR(128) NULL,
        StageType NVARCHAR(64) NULL,
        CONSTRAINT FK_WorkflowStepInstances_{suffix}_WorkflowInstance FOREIGN KEY (WorkflowInstanceId) REFERENCES workflow.WorkflowInstances_{suffix}(Id) ON DELETE CASCADE
    );
END
{EnsureNonClusteredIndex($"workflow.WorkflowStepInstances_{suffix}", $"IX_WorkflowStepInstances_{suffix}_WorkflowInstanceId_Order", "WorkflowInstanceId, [Order]")}
IF OBJECT_ID(N'workflow.WorkflowStepInstances_{suffix}', N'U') IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'workflow.WorkflowStepInstances_{suffix}') AND name = N'ActivityId')
BEGIN
    ALTER TABLE workflow.WorkflowStepInstances_{suffix} ADD ActivityId NVARCHAR(128) NULL;
END
IF OBJECT_ID(N'workflow.WorkflowStepInstances_{suffix}', N'U') IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'workflow.WorkflowStepInstances_{suffix}') AND name = N'StageType')
BEGIN
    ALTER TABLE workflow.WorkflowStepInstances_{suffix} ADD StageType NVARCHAR(64) NULL;
END";
    }

    private static string GenerateWorkflowInstanceSlasTableScript(string suffix)
    {
        return $@"
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'WorkflowInstanceSlas_{suffix}' AND schema_id = SCHEMA_ID('workflow'))
BEGIN
    CREATE TABLE workflow.WorkflowInstanceSlas_{suffix} (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        WorkflowInstanceId UNIQUEIDENTIFIER NOT NULL,
        Priority INT NOT NULL,
        ResponseDeadline DATETIME2 NOT NULL,
        ResolutionDeadline DATETIME2 NOT NULL,
        EscalationDeadline DATETIME2 NULL,
        ResponseAchievedAt DATETIME2 NULL,
        ResolutionAchievedAt DATETIME2 NULL,
        ResponseStatus INT NOT NULL,
        ResolutionStatus INT NOT NULL,
        IsEscalated BIT NOT NULL DEFAULT 0,
        EscalatedAt DATETIME2 NULL,
        CreatedAtUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_WorkflowInstanceSlas_{suffix}_WorkflowInstance FOREIGN KEY (WorkflowInstanceId) REFERENCES workflow.WorkflowInstances_{suffix}(Id) ON DELETE CASCADE,
        CONSTRAINT UQ_WorkflowInstanceSlas_{suffix}_WorkflowInstanceId UNIQUE (WorkflowInstanceId)
    );
END
{EnsureNonClusteredIndex($"workflow.WorkflowInstanceSlas_{suffix}", $"IX_WorkflowInstanceSlas_{suffix}_ResponseStatus_ResolutionStatus", "ResponseStatus, ResolutionStatus")}";
    }

    private static string GenerateWorkflowInstanceUserStateTableScript(string suffix)
    {
        return $@"
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'WorkflowInstanceUserState_{suffix}' AND schema_id = SCHEMA_ID('workflow'))
BEGIN
    CREATE TABLE workflow.WorkflowInstanceUserState_{suffix} (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        WorkflowInstanceId UNIQUEIDENTIFIER NOT NULL,
        UserId UNIQUEIDENTIFIER NOT NULL,
        IsRead BIT NOT NULL DEFAULT 0,
        ReadAtUtc DATETIME2 NULL,
        IsActioned BIT NOT NULL DEFAULT 0,
        ActionedAtUtc DATETIME2 NULL,
        CreatedAtUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END
{EnsureNonClusteredIndex($"workflow.WorkflowInstanceUserState_{suffix}", $"IX_WorkflowInstanceUserState_{suffix}_WorkflowInstanceId_UserId", "WorkflowInstanceId, UserId")}";
    }

    private static string GenerateLegacyTransactionTableScript(string suffix)
    {
        var idx = suffix.Replace("-", "_");
        return $@"
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'transaction_{suffix}' AND schema_id = SCHEMA_ID('workflow'))
BEGIN
    CREATE TABLE workflow.[transaction_{suffix}] (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        TransactionGuid UNIQUEIDENTIFIER NULL,
        WorkflowInstanceId UNIQUEIDENTIFIER NOT NULL,
        ActivityId NVARCHAR(128) NULL,
        RuleId NVARCHAR(128) NULL,
        StageType NVARCHAR(64) NULL,
        StageName NVARCHAR(256) NULL,
        Review NVARCHAR(64) NULL,
        ActionStatus INT NOT NULL DEFAULT 0,
        ActivityUserId UNIQUEIDENTIFIER NULL,
        ActivityGroupId INT NULL,
        UserIds NVARCHAR(MAX) NULL,        -- v5 parity (comma-separated legacy ids)
        GroupIds NVARCHAR(MAX) NULL,       -- v5 parity (comma-separated legacy ids)
        SlaTransactionId INT NULL,         -- v5 parity
        InputFrom NVARCHAR(64) NULL,
        LevelId INT NULL,
        UserType NVARCHAR(64) NULL,
        JiraIssueJson NVARCHAR(MAX) NULL,
        MlPrediction NVARCHAR(MAX) NULL,
        MlCondition NVARCHAR(MAX) NULL,
        TicketLockedBy UNIQUEIDENTIFIER NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedBy UNIQUEIDENTIFIER NULL,
        ModifiedAt DATETIME2 NULL,
        ModifiedBy UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL DEFAULT 0
    );
END
{EnsureNonClusteredIndex($"workflow.[transaction_{suffix}]", $"IX_transaction_{idx}_WorkflowInstanceId_IsDeleted", "WorkflowInstanceId, IsDeleted")}
{EnsureNonClusteredIndex($"workflow.[transaction_{suffix}]", $"IX_transaction_{idx}_ActivityUser_ActionStatus", "ActivityUserId, ActionStatus")}
{EnsureNonClusteredIndex($"workflow.[transaction_{suffix}]", $"IX_transaction_{idx}_Instance_Status", "WorkflowInstanceId, IsDeleted, ActionStatus", "ActivityUserId, CreatedBy, StageType, ActivityGroupId")}";
    }

    private static string GenerateCommentsTableScript(string suffix)
    {
        return $@"
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'WorkflowComments_{suffix}' AND schema_id = SCHEMA_ID('workflow'))
BEGIN
    CREATE TABLE workflow.WorkflowComments_{suffix} (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        TenantId UNIQUEIDENTIFIER NOT NULL,
        WorkflowInstanceId UNIQUEIDENTIFIER NOT NULL,
        StepInstanceId UNIQUEIDENTIFIER NULL,
        Comments NVARCHAR(4000) NOT NULL,
        ExternalCommentsBy NVARCHAR(256) NULL,
        ShowTo INT NOT NULL DEFAULT 0,
        EmbedJson NVARCHAR(4000) NULL,
        EmbedStatus BIT NOT NULL DEFAULT 0,
        CreatedAtUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        ModifiedAtUtc DATETIME2 NULL,
        CreatedBy UNIQUEIDENTIFIER NOT NULL,
        ModifiedBy UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL DEFAULT 0
    );
END
{EnsureNonClusteredIndex($"workflow.WorkflowComments_{suffix}", $"IX_WorkflowComments_{suffix}_TenantId_WorkflowInstanceId", "TenantId, WorkflowInstanceId, IsDeleted")}";
    }

    private static string GenerateAttachmentsTableScript(string suffix)
    {
        var tableName = $"WorkflowAttachments_{suffix}";
        var tableFull = $"workflow.[WorkflowAttachments_{suffix}]";
        var tableQualified = $"workflow.{tableName}";

        return $@"
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = '{tableName}' AND schema_id = SCHEMA_ID('workflow'))
BEGIN
    CREATE TABLE {tableFull} (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        TenantId UNIQUEIDENTIFIER NOT NULL,
        WorkflowInstanceId UNIQUEIDENTIFIER NOT NULL,
        StepInstanceId UNIQUEIDENTIFIER NULL,
        RepositoryId UNIQUEIDENTIFIER NULL,
        ItemId UNIQUEIDENTIFIER NULL,
        FormJsonId NVARCHAR(128) NULL,
        FileName NVARCHAR(512) NULL,
        FilePath NVARCHAR(1024) NULL,
        FileSize BIGINT NULL,
        ContentType NVARCHAR(128) NULL,
        CreatedAtUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        ModifiedAtUtc DATETIME2 NULL,
        CreatedBy UNIQUEIDENTIFIER NOT NULL,
        ModifiedBy UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL DEFAULT 0
    );
END
{EnsureNonClusteredIndex(tableFull, $"IX_{tableName}_TenantId_WorkflowInstanceId", "TenantId, WorkflowInstanceId, IsDeleted")}
ELSE
BEGIN
    IF COL_LENGTH('{tableQualified}', 'RepositoryGuid') IS NOT NULL
        ALTER TABLE {tableFull} DROP COLUMN RepositoryGuid;
    IF COL_LENGTH('{tableQualified}', 'ItemGuid') IS NOT NULL
        ALTER TABLE {tableFull} DROP COLUMN ItemGuid;

    IF COL_LENGTH('{tableQualified}', 'RepositoryId') IS NULL
        ALTER TABLE {tableFull} ADD RepositoryId UNIQUEIDENTIFIER NULL;
    ELSE IF EXISTS (
        SELECT 1 FROM sys.columns c
        INNER JOIN sys.types ty ON ty.user_type_id = c.user_type_id
        INNER JOIN sys.tables t ON t.object_id = c.object_id
        INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
        WHERE s.name = N'workflow' AND t.name = N'{tableName}'
          AND c.name = N'RepositoryId' AND ty.name IN (N'int', N'bigint', N'smallint'))
    BEGIN
        ALTER TABLE {tableFull} DROP COLUMN RepositoryId;
        ALTER TABLE {tableFull} ADD RepositoryId UNIQUEIDENTIFIER NULL;
    END

    IF COL_LENGTH('{tableQualified}', 'ItemId') IS NULL
        ALTER TABLE {tableFull} ADD ItemId UNIQUEIDENTIFIER NULL;
    ELSE IF EXISTS (
        SELECT 1 FROM sys.columns c
        INNER JOIN sys.types ty ON ty.user_type_id = c.user_type_id
        INNER JOIN sys.tables t ON t.object_id = c.object_id
        INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
        WHERE s.name = N'workflow' AND t.name = N'{tableName}'
          AND c.name = N'ItemId' AND ty.name IN (N'int', N'bigint', N'smallint'))
    BEGIN
        ALTER TABLE {tableFull} DROP COLUMN ItemId;
        ALTER TABLE {tableFull} ADD ItemId UNIQUEIDENTIFIER NULL;
    END
END";
    }

    /// <summary>Legacy v5 processForm_{suffix}: WorkflowInstanceId + ezfb itemId as FormEntryId (no ProcessId).</summary>
    private static string GenerateProcessFormTableScript(string suffix)
    {
        var idx = suffix.Replace("-", "_");
        return $@"
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'processForm_{suffix}' AND schema_id = SCHEMA_ID('workflow'))
BEGIN
    CREATE TABLE workflow.processForm_{suffix} (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        WorkflowInstanceId UNIQUEIDENTIFIER NOT NULL,
        WFormId NVARCHAR(64) NOT NULL,
        FormEntryId INT NOT NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_processForm_{idx}_CreatedAt DEFAULT SYSUTCDATETIME(),
        CreatedBy UNIQUEIDENTIFIER NOT NULL,
        IsDeleted BIT NOT NULL CONSTRAINT DF_processForm_{idx}_IsDeleted DEFAULT 0
    );
END
{EnsureNonClusteredIndex($"workflow.processForm_{suffix}", $"IX_processForm_{idx}_WorkflowInstanceId_IsDeleted", "WorkflowInstanceId, IsDeleted")}";
    }

    /// <summary>Legacy v5 processAddon_{suffix}: process (instance) → repository item link for attachments.</summary>
    private static string GenerateProcessAddonTableScript(string suffix)
    {
        var idx = suffix.Replace("-", "_");
        return $@"
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'processAddon_{suffix}' AND schema_id = SCHEMA_ID('workflow'))
BEGIN
    CREATE TABLE workflow.processAddon_{suffix} (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        ProcessId UNIQUEIDENTIFIER NOT NULL,
        RepositoryId UNIQUEIDENTIFIER NOT NULL,
        ItemId UNIQUEIDENTIFIER NOT NULL,
        FileName NVARCHAR(512) NULL,
        TransactionId INT NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_processAddon_{idx}_CreatedAt DEFAULT SYSUTCDATETIME(),
        ModifiedAt DATETIME2 NULL,
        CreatedBy UNIQUEIDENTIFIER NOT NULL,
        ModifiedBy UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL CONSTRAINT DF_processAddon_{idx}_IsDeleted DEFAULT 0
    );
END
{EnsureNonClusteredIndex($"workflow.processAddon_{suffix}", $"IX_processAddon_{idx}_ProcessId_IsDeleted", "ProcessId, IsDeleted")}";
    }

    private static string GenerateFormsTableScript(string suffix)
    {
        return $@"
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'WorkflowForms_{suffix}' AND schema_id = SCHEMA_ID('workflow'))
BEGIN
    CREATE TABLE workflow.WorkflowForms_{suffix} (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        TenantId UNIQUEIDENTIFIER NOT NULL,
        WorkflowInstanceId UNIQUEIDENTIFIER NOT NULL,
        StepInstanceId UNIQUEIDENTIFIER NULL,
        WFormId INT NOT NULL,
        FormEntryId INT NOT NULL,
        FormData NVARCHAR(4000) NULL,
        HasFormPdf BIT NOT NULL DEFAULT 0,
        CreatedAtUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        ModifiedAtUtc DATETIME2 NULL,
        CreatedBy UNIQUEIDENTIFIER NOT NULL,
        ModifiedBy UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL DEFAULT 0
    );
END
{EnsureNonClusteredIndex($"workflow.WorkflowForms_{suffix}", $"IX_WorkflowForms_{suffix}_TenantId_WorkflowInstanceId", "TenantId, WorkflowInstanceId, IsDeleted")}";
    }

    private static string GenerateTasksTableScript(string suffix)
    {
        return $@"
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'WorkflowTasks_{suffix}' AND schema_id = SCHEMA_ID('workflow'))
BEGIN
    CREATE TABLE workflow.WorkflowTasks_{suffix} (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        TenantId UNIQUEIDENTIFIER NOT NULL,
        WorkflowInstanceId UNIQUEIDENTIFIER NOT NULL,
        StepInstanceId UNIQUEIDENTIFIER NULL,
        WFormId INT NOT NULL,
        FormEntryId INT NOT NULL,
        TaskName NVARCHAR(256) NULL,
        TaskDescription NVARCHAR(2000) NULL,
        AssignedToUserId UNIQUEIDENTIFIER NULL,
        DueDate DATETIME2 NULL,
        IsCompleted BIT NOT NULL DEFAULT 0,
        CompletedAt DATETIME2 NULL,
        CreatedAtUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        ModifiedAtUtc DATETIME2 NULL,
        CreatedBy UNIQUEIDENTIFIER NOT NULL,
        ModifiedBy UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL DEFAULT 0
    );
END
{EnsureNonClusteredIndex($"workflow.WorkflowTasks_{suffix}", $"IX_WorkflowTasks_{suffix}_TenantId_WorkflowInstanceId", "TenantId, WorkflowInstanceId, IsDeleted")}
{EnsureNonClusteredIndex($"workflow.WorkflowTasks_{suffix}", $"IX_WorkflowTasks_{suffix}_TenantId_AssignedToUserId", "TenantId, AssignedToUserId, IsCompleted")}";
    }

    private static string GenerateSignaturesTableScript(string suffix)
    {
        return $@"
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'WorkflowSignatures_{suffix}' AND schema_id = SCHEMA_ID('workflow'))
BEGIN
    CREATE TABLE workflow.WorkflowSignatures_{suffix} (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        TenantId UNIQUEIDENTIFIER NOT NULL,
        WorkflowInstanceId UNIQUEIDENTIFIER NOT NULL,
        StepInstanceId UNIQUEIDENTIFIER NULL,
        FileName NVARCHAR(512) NOT NULL,
        FilePath NVARCHAR(1024) NULL,
        SignedBy UNIQUEIDENTIFIER NOT NULL,
        SignedAtUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        SignatureData NVARCHAR(4000) NULL,
        CreatedAtUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        ModifiedAtUtc DATETIME2 NULL,
        CreatedBy UNIQUEIDENTIFIER NOT NULL,
        ModifiedBy UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL DEFAULT 0
    );
END
{EnsureNonClusteredIndex($"workflow.WorkflowSignatures_{suffix}", $"IX_WorkflowSignatures_{suffix}_TenantId_WorkflowInstanceId", "TenantId, WorkflowInstanceId, IsDeleted")}";
    }

    private static string GenerateDocumentsTableScript(string suffix)
    {
        return $@"
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'WorkflowDocuments_{suffix}' AND schema_id = SCHEMA_ID('workflow'))
BEGIN
    CREATE TABLE workflow.WorkflowDocuments_{suffix} (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        TenantId UNIQUEIDENTIFIER NOT NULL,
        WorkflowId UNIQUEIDENTIFIER NOT NULL,
        WorkflowInstanceId UNIQUEIDENTIFIER NULL,
        FileName NVARCHAR(512) NOT NULL,
        Description NVARCHAR(2000) NULL,
        Type NVARCHAR(64) NULL,
        Status INT NOT NULL DEFAULT 0,
        IsMandatory BIT NOT NULL DEFAULT 0,
        FilePath NVARCHAR(1024) NULL,
        UploadedAt DATETIME2 NULL,
        CreatedAtUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        ModifiedAtUtc DATETIME2 NULL,
        CreatedBy UNIQUEIDENTIFIER NOT NULL,
        ModifiedBy UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL DEFAULT 0
    );
END
{EnsureNonClusteredIndex($"workflow.WorkflowDocuments_{suffix}", $"IX_WorkflowDocuments_{suffix}_TenantId_WorkflowId", "TenantId, WorkflowId, IsDeleted")}
{EnsureNonClusteredIndex($"workflow.WorkflowDocuments_{suffix}", $"IX_WorkflowDocuments_{suffix}_TenantId_WorkflowInstanceId", "TenantId, WorkflowInstanceId, IsDeleted")}";
    }

    private static string GenerateEmailsTableScript(string suffix)
    {
        return $@"
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'WorkflowEmails_{suffix}' AND schema_id = SCHEMA_ID('workflow'))
BEGIN
    CREATE TABLE workflow.WorkflowEmails_{suffix} (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        TenantId UNIQUEIDENTIFIER NOT NULL,
        WorkflowInstanceId UNIQUEIDENTIFIER NOT NULL,
        StepInstanceId UNIQUEIDENTIFIER NULL,
        EmailType NVARCHAR(64) NOT NULL,
        EzMailId INT NULL,
        MsgFileName NVARCHAR(512) NULL,
        Email NVARCHAR(256) NOT NULL,
        Subject NVARCHAR(512) NULL,
        Body NVARCHAR(4000) NULL,
        AttachmentCount INT NOT NULL DEFAULT 0,
        AttachmentJson NVARCHAR(4000) NULL,
        CreatedAtUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        ModifiedAtUtc DATETIME2 NULL,
        CreatedBy UNIQUEIDENTIFIER NOT NULL,
        ModifiedBy UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL DEFAULT 0
    );
END
{EnsureNonClusteredIndex($"workflow.WorkflowEmails_{suffix}", $"IX_WorkflowEmails_{suffix}_TenantId_WorkflowInstanceId", "TenantId, WorkflowInstanceId, IsDeleted")}";
    }

    /// <summary>Legacy AP agent OCR/validation store (agentDataValidation_{workflow8}).</summary>
    private static string GenerateAgentDataValidationTableScript(string suffix)
    {
        return $@"
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'agentDataValidation_{suffix}' AND schema_id = SCHEMA_ID('workflow'))
BEGIN
    CREATE TABLE workflow.agentDataValidation_{suffix} (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        WorkflowId UNIQUEIDENTIFIER NOT NULL,
        ProcessId UNIQUEIDENTIFIER NOT NULL,
        TransactionId NVARCHAR(256) NULL,
        Type NVARCHAR(64) NULL,
        AgentResponse NVARCHAR(MAX) NULL,
        AgentHtmlResponse NVARCHAR(MAX) NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_agentDataValidation_{suffix}_CreatedAt DEFAULT SYSUTCDATETIME(),
        ModifiedAt DATETIME2 NULL,
        CreatedBy UNIQUEIDENTIFIER NOT NULL,
        ModifiedBy UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL CONSTRAINT DF_agentDataValidation_{suffix}_IsDeleted DEFAULT 0
    );
END";
    }

    private static string GenerateAiValidationsTableScript(string suffix)
    {
        return $@"
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'WorkflowAiValidations_{suffix}' AND schema_id = SCHEMA_ID('workflow'))
BEGIN
    CREATE TABLE workflow.WorkflowAiValidations_{suffix} (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        TenantId UNIQUEIDENTIFIER NOT NULL,
        WorkflowInstanceId UNIQUEIDENTIFIER NOT NULL,
        StepInstanceId UNIQUEIDENTIFIER NULL,
        Type NVARCHAR(64) NOT NULL,
        AgentResponse NVARCHAR(4000) NULL,
        FieldName NVARCHAR(256) NULL,
        FormValue NVARCHAR(1000) NULL,
        OcrValue NVARCHAR(1000) NULL,
        ValidationStatus NVARCHAR(64) NULL,
        CreatedAtUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        ModifiedAtUtc DATETIME2 NULL,
        CreatedBy UNIQUEIDENTIFIER NOT NULL,
        ModifiedBy UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL DEFAULT 0
    );
END
{EnsureNonClusteredIndex($"workflow.WorkflowAiValidations_{suffix}", $"IX_WorkflowAiValidations_{suffix}_TenantId_WorkflowInstanceId", "TenantId, WorkflowInstanceId, IsDeleted")}";
    }

    private static string GeneratePdfAnnotationsTableScript(string suffix)
    {
        return $@"
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'WorkflowPdfAnnotations_{suffix}' AND schema_id = SCHEMA_ID('workflow'))
BEGIN
    CREATE TABLE workflow.WorkflowPdfAnnotations_{suffix} (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        TenantId UNIQUEIDENTIFIER NOT NULL,
        WorkflowInstanceId UNIQUEIDENTIFIER NOT NULL,
        StepInstanceId UNIQUEIDENTIFIER NULL,
        RepositoryId INT NULL,
        ItemId INT NULL,
        AnnotationStatus INT NOT NULL DEFAULT 0,
        SettingsJson NVARCHAR(4000) NULL,
        CreatedAtUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        ModifiedAtUtc DATETIME2 NULL,
        CreatedBy UNIQUEIDENTIFIER NOT NULL,
        ModifiedBy UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL DEFAULT 0
    );
END
{EnsureNonClusteredIndex($"workflow.WorkflowPdfAnnotations_{suffix}", $"IX_WorkflowPdfAnnotations_{suffix}_TenantId_WorkflowInstanceId", "TenantId, WorkflowInstanceId, IsDeleted")}";
    }

    /// <summary>Separate CREATE INDEX (inline INDEX in CREATE TABLE fails on SQL Server &lt; 2014).</summary>
    private static string EnsureNonClusteredIndex(
        string tableObjectName,
        string indexName,
        string columnList,
        string? includeColumns = null)
    {
        var include = string.IsNullOrWhiteSpace(includeColumns) ? "" : $" INCLUDE ({includeColumns})";
        return $@"
IF OBJECT_ID(N'{tableObjectName}', N'U') IS NOT NULL
  AND NOT EXISTS (
    SELECT 1 FROM sys.indexes i
    WHERE i.name = N'{indexName}' AND i.object_id = OBJECT_ID(N'{tableObjectName}'))
    CREATE NONCLUSTERED INDEX [{indexName}] ON {tableObjectName} ({columnList}){include};";
    }
}
