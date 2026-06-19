using Microsoft.Data.SqlClient;
using SaaSApp.Workflow.Application;
using SaaSApp.Workflow.Application.Contracts;

namespace SaaSApp.Workflow.Infrastructure.Services;

public sealed class ApAgentJobProgressService : IApAgentJobProgressService
{
    private const string TableName = "workflow.ApAgentJobProgress";

    private readonly ITenantContext _tenantContext;

    public ApAgentJobProgressService(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    public async Task RegisterQueuedAsync(
        string jobId,
        Guid tenantId,
        Guid workflowId,
        Guid instanceId,
        CancellationToken cancellationToken = default)
    {
        await EnsureTableAsync(cancellationToken);
        const string sql = $"""
            INSERT INTO {TableName}
                (JobId, TenantId, WorkflowId, InstanceId, HangfireState, Stage, Message, ProgressPercent, ErrorMessage, CreatedAtUtc, UpdatedAtUtc)
            VALUES
                (@JobId, @TenantId, @WorkflowId, @InstanceId, N'Enqueued', N'QUEUED', N'AP Agent job queued', NULL, NULL, SYSUTCDATETIME(), SYSUTCDATETIME());
            """;

        await ExecuteAsync(sql, cmd =>
        {
            cmd.Parameters.AddWithValue("@JobId", jobId);
            cmd.Parameters.AddWithValue("@TenantId", tenantId);
            cmd.Parameters.AddWithValue("@WorkflowId", workflowId);
            cmd.Parameters.AddWithValue("@InstanceId", instanceId);
        }, cancellationToken);
    }

    public Task UpdateProgressAsync(
        string jobId,
        ApAgentJobProgressUpdate update,
        CancellationToken cancellationToken = default) =>
        ApplyProgressUpdateAsync(jobId, update, cancellationToken);

    public async Task UpdateProgressByInstanceAsync(
        Guid workflowId,
        Guid instanceId,
        ApAgentJobProgressUpdate update,
        CancellationToken cancellationToken = default)
    {
        var jobId = await GetLatestActiveJobIdForInstanceAsync(instanceId, cancellationToken);
        if (string.IsNullOrWhiteSpace(jobId))
            throw new InvalidOperationException($"No active AP Agent job found for instance {instanceId:D}.");

        await ApplyProgressUpdateAsync(jobId, update, cancellationToken);
    }

    public async Task UpdateFormDataByInstanceAsync(
        Guid workflowId,
        Guid instanceId,
        string formDataJson,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(formDataJson))
            return;

        var jobId = await GetLatestJobIdForInstanceAsync(workflowId, instanceId, cancellationToken);
        if (string.IsNullOrWhiteSpace(jobId))
            throw new InvalidOperationException(
                $"No AP Agent job found for workflow {workflowId:D} instance {instanceId:D}.");

        await ApplyProgressUpdateAsync(
            jobId,
            new ApAgentJobProgressUpdate(FormData: formDataJson),
            cancellationToken);
    }

    private static bool IsTerminalStage(string? stage) =>
        string.Equals(stage, "COMPLETED", StringComparison.OrdinalIgnoreCase)
        || string.Equals(stage, "FAILED", StringComparison.OrdinalIgnoreCase);

    private static bool IsTerminalHangfireState(string? state) =>
        string.Equals(state, "Succeeded", StringComparison.OrdinalIgnoreCase)
        || string.Equals(state, "Failed", StringComparison.OrdinalIgnoreCase);

    public async Task SetHangfireStateAsync(
        string jobId,
        string hangfireState,
        string? message = null,
        string? errorMessage = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsTerminalHangfireState(hangfireState))
        {
            var existing = await GetByJobIdAsync(jobId, cancellationToken);
            if (existing != null && IsTerminalHangfireState(existing.HangfireState))
                return;
        }

        await EnsureTableAsync(cancellationToken);
        const string sql = $"""
            UPDATE {TableName}
            SET HangfireState = @HangfireState,
                Message = COALESCE(@Message, Message),
                ErrorMessage = @ErrorMessage,
                Stage = CASE
                    WHEN @HangfireState = N'Succeeded' THEN COALESCE(Stage, N'COMPLETED')
                    WHEN @HangfireState = N'Failed' THEN COALESCE(Stage, N'FAILED')
                    ELSE Stage
                END,
                UpdatedAtUtc = SYSUTCDATETIME()
            WHERE JobId = @JobId;
            """;

        await ExecuteAsync(sql, cmd =>
        {
            cmd.Parameters.AddWithValue("@JobId", jobId);
            cmd.Parameters.AddWithValue("@HangfireState", hangfireState);
            cmd.Parameters.AddWithValue("@Message", (object?)message ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ErrorMessage", (object?)errorMessage ?? DBNull.Value);
        }, cancellationToken);
    }

    public async Task<ApAgentJobProgressRow?> GetByJobIdAsync(string jobId, CancellationToken cancellationToken = default)
    {
        await EnsureTableAsync(cancellationToken);
        const string sql = $"""
            SELECT TOP 1 JobId, TenantId, WorkflowId, InstanceId, HangfireState, Stage, Message, ProgressPercent, ErrorMessage, FormData, UpdatedAtUtc
            FROM {TableName}
            WHERE JobId = @JobId;
            """;

        await using var connection = OpenConnection();
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@JobId", jobId);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return MapRow(reader);
    }

    public async Task<string?> GetLatestActiveJobIdForInstanceAsync(
        Guid instanceId,
        CancellationToken cancellationToken = default)
    {
        await EnsureTableAsync(cancellationToken);
        const string sql = $"""
            SELECT TOP 1 JobId
            FROM {TableName}
            WHERE InstanceId = @InstanceId
              AND HangfireState IN (N'Enqueued', N'Processing')
            ORDER BY UpdatedAtUtc DESC;
            """;

        await using var connection = OpenConnection();
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@InstanceId", instanceId);
        var value = await cmd.ExecuteScalarAsync(cancellationToken);
        return value == null || value == DBNull.Value ? null : Convert.ToString(value);
    }

    public async Task<string?> GetLatestJobIdForInstanceAsync(
        Guid workflowId,
        Guid instanceId,
        CancellationToken cancellationToken = default)
    {
        await EnsureTableAsync(cancellationToken);
        const string sql = $"""
            SELECT TOP 1 JobId
            FROM {TableName}
            WHERE WorkflowId = @WorkflowId
              AND InstanceId = @InstanceId
            ORDER BY UpdatedAtUtc DESC;
            """;

        await using var connection = OpenConnection();
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@WorkflowId", workflowId);
        cmd.Parameters.AddWithValue("@InstanceId", instanceId);
        var value = await cmd.ExecuteScalarAsync(cancellationToken);
        return value == null || value == DBNull.Value ? null : Convert.ToString(value);
    }

    private async Task ApplyProgressUpdateAsync(
        string jobId,
        ApAgentJobProgressUpdate update,
        CancellationToken cancellationToken)
    {
        await EnsureTableAsync(cancellationToken);

        var existing = await GetByJobIdAsync(jobId, cancellationToken);
        if (existing != null
            && (IsTerminalStage(existing.Stage) || IsTerminalHangfireState(existing.HangfireState))
            && !string.IsNullOrWhiteSpace(update.Stage)
            && !IsTerminalStage(update.Stage))
        {
            // Ignore late non-terminal updates after COMPLETED/FAILED (e.g. duplicate PROCESSING PATCH).
            update = update with { Stage = null, Message = null, Percent = null };
            if (update.FormData == null)
                return;
        }

        const string sql = $"""
            UPDATE {TableName}
            SET Stage = COALESCE(@Stage, Stage),
                Message = COALESCE(@Message, Message),
                ProgressPercent = COALESCE(@ProgressPercent, ProgressPercent),
                FormData = COALESCE(@FormData, FormData),
                UpdatedAtUtc = SYSUTCDATETIME()
            WHERE JobId = @JobId;
            """;

        await ExecuteAsync(sql, cmd =>
        {
            cmd.Parameters.AddWithValue("@JobId", jobId);
            cmd.Parameters.AddWithValue("@Stage", (object?)update.Stage ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Message", (object?)update.Message ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ProgressPercent", (object?)update.Percent ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@FormData", (object?)update.FormData ?? DBNull.Value);
        }, cancellationToken);

        if (string.IsNullOrWhiteSpace(update.Stage))
            return;

        if (string.Equals(update.Stage, "COMPLETED", StringComparison.OrdinalIgnoreCase))
        {
            await SetHangfireStateAsync(
                jobId,
                "Succeeded",
                update.Message ?? "AP Agent finished successfully",
                cancellationToken: cancellationToken);
        }
        else if (string.Equals(update.Stage, "FAILED", StringComparison.OrdinalIgnoreCase))
        {
            await SetHangfireStateAsync(
                jobId,
                "Failed",
                update.Message ?? "AP Agent failed",
                update.Message,
                cancellationToken);
        }
    }

    private async Task EnsureTableAsync(CancellationToken cancellationToken)
    {
        const string ensureSchemaSql = """
            IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'workflow')
                EXEC(N'CREATE SCHEMA workflow');
            """;

        const string createTableSql = """
            IF NOT EXISTS (
                SELECT 1 FROM sys.tables t
                INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
                WHERE s.name = N'workflow' AND t.name = N'ApAgentJobProgress')
            BEGIN
                CREATE TABLE workflow.ApAgentJobProgress (
                    JobId NVARCHAR(64) NOT NULL PRIMARY KEY,
                    TenantId UNIQUEIDENTIFIER NOT NULL,
                    WorkflowId UNIQUEIDENTIFIER NOT NULL,
                    InstanceId UNIQUEIDENTIFIER NOT NULL,
                    HangfireState NVARCHAR(32) NULL,
                    Stage NVARCHAR(64) NULL,
                    Message NVARCHAR(2000) NULL,
                    ProgressPercent INT NULL,
                    ErrorMessage NVARCHAR(MAX) NULL,
                    FormData NVARCHAR(MAX) NULL,
                    CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_ApAgentJobProgress_CreatedAt DEFAULT SYSUTCDATETIME(),
                    UpdatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_ApAgentJobProgress_UpdatedAt DEFAULT SYSUTCDATETIME()
                );
            END
            """;

        const string addFormDataColumnSql = """
            IF EXISTS (
                SELECT 1 FROM sys.tables t
                INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
                WHERE s.name = N'workflow' AND t.name = N'ApAgentJobProgress')
               AND COL_LENGTH(N'workflow.ApAgentJobProgress', N'FormData') IS NULL
            BEGIN
                ALTER TABLE workflow.ApAgentJobProgress ADD FormData NVARCHAR(MAX) NULL;
            END
            """;

        const string createIndexSql = """
            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes i
                INNER JOIN sys.tables t ON t.object_id = i.object_id
                INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
                WHERE s.name = N'workflow'
                  AND t.name = N'ApAgentJobProgress'
                  AND i.name = N'IX_ApAgentJobProgress_InstanceId_Updated')
            BEGIN
                CREATE INDEX IX_ApAgentJobProgress_InstanceId_Updated
                    ON workflow.ApAgentJobProgress (InstanceId, UpdatedAtUtc DESC);
            END
            """;

        await using var connection = OpenConnection();
        foreach (var batch in new[] { ensureSchemaSql, createTableSql, addFormDataColumnSql, createIndexSql })
        {
            await using var cmd = new SqlCommand(batch, connection);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private SqlConnection OpenConnection()
    {
        var connectionString = _tenantContext.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Tenant connection string not resolved.");

        var connection = new SqlConnection(connectionString);
        connection.Open();
        return connection;
    }

    private async Task ExecuteAsync(
        string sql,
        Action<SqlCommand> configure,
        CancellationToken cancellationToken)
    {
        await using var connection = OpenConnection();
        await using var cmd = new SqlCommand(sql, connection);
        configure(cmd);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static ApAgentJobProgressRow MapRow(SqlDataReader reader) =>
        new(
            reader.GetString(0),
            reader.GetGuid(1),
            reader.GetGuid(2),
            reader.GetGuid(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.IsDBNull(7) ? null : reader.GetInt32(7),
            reader.IsDBNull(8) ? null : reader.GetString(8),
            reader.IsDBNull(9) ? null : reader.GetString(9),
            reader.GetDateTime(10));
}
