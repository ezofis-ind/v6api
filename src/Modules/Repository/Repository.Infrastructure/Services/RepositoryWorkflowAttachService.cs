using Microsoft.Data.SqlClient;
using SaaSApp.MultiTenancy;

namespace SaaSApp.Repository.Infrastructure.Services;

public sealed class RepositoryWorkflowAttachService
{
    private readonly ITenantConnectionProvider _connectionProvider;
    private readonly ITenantProvider _tenantProvider;

    public RepositoryWorkflowAttachService(
        ITenantConnectionProvider connectionProvider,
        ITenantProvider tenantProvider)
    {
        _connectionProvider = connectionProvider;
        _tenantProvider = tenantProvider;
    }

    /// <summary>
    /// Inserts a row into workflow.WorkflowAttachments_{suffix} for this instance (start upload / repository link).
    /// </summary>
    public async Task<Guid> AttachAsync(
        Guid workflowId,
        Guid workflowInstanceId,
        int? transactionId,
        Guid repositoryId,
        Guid itemId,
        string fileName,
        string filePath,
        long? fileSize,
        string? contentType,
        Guid? userId,
        Guid? stepInstanceId = null,
        CancellationToken cancellationToken = default)
    {
        _ = transactionId;

        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name is required.", nameof(fileName));
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path is required.", nameof(filePath));

        var connectionString = _connectionProvider.ConnectionString
            ?? throw new InvalidOperationException("Tenant connection string not resolved.");
        var tenantId = _tenantProvider.GetTenantId()
            ?? throw new InvalidOperationException("Tenant context is required.");
        var createdBy = userId ?? Guid.Empty;

        var suffix = workflowId.ToString("N")[..8];
        var instancesTable = $"workflow.[WorkflowInstances_{suffix}]";
        var attachmentsTable = $"workflow.[WorkflowAttachments_{suffix}]";

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var existsSql = $"SELECT 1 FROM {instancesTable} WHERE Id = @WorkflowInstanceId;";
        await using (var existsCmd = new SqlCommand(existsSql, connection))
        {
            existsCmd.Parameters.AddWithValue("@WorkflowInstanceId", workflowInstanceId);
            var exists = await existsCmd.ExecuteScalarAsync(cancellationToken);
            if (exists == null)
                throw new InvalidOperationException($"Workflow instance {workflowInstanceId} not found for workflow {workflowId}.");
        }

        var formJsonId = itemId.ToString("N");
        var attachmentId = Guid.NewGuid();

        var insertSql = $"""
            INSERT INTO {attachmentsTable}
                (Id, TenantId, WorkflowInstanceId, StepInstanceId, RepositoryId, ItemId, FormJsonId,
                 FileName, FilePath, FileSize, ContentType, CreatedAtUtc, ModifiedAtUtc, CreatedBy, ModifiedBy, IsDeleted)
            VALUES
                (@Id, @TenantId, @WorkflowInstanceId, @StepInstanceId, @RepositoryId, @ItemId, @FormJsonId,
                 @FileName, @FilePath, @FileSize, @ContentType, SYSUTCDATETIME(), SYSUTCDATETIME(), @CreatedBy, @ModifiedBy, 0);
            """;

        await using var insertCmd = new SqlCommand(insertSql, connection);
        insertCmd.Parameters.AddWithValue("@Id", attachmentId);
        insertCmd.Parameters.AddWithValue("@TenantId", tenantId);
        insertCmd.Parameters.AddWithValue("@WorkflowInstanceId", workflowInstanceId);
        insertCmd.Parameters.AddWithValue("@StepInstanceId", (object?)stepInstanceId ?? DBNull.Value);
        insertCmd.Parameters.AddWithValue("@RepositoryId", repositoryId);
        insertCmd.Parameters.AddWithValue("@ItemId", itemId);
        insertCmd.Parameters.AddWithValue("@FormJsonId", formJsonId);
        insertCmd.Parameters.AddWithValue("@FileName", fileName.Trim());
        insertCmd.Parameters.AddWithValue("@FilePath", filePath.Trim());
        insertCmd.Parameters.AddWithValue("@FileSize", (object?)fileSize ?? DBNull.Value);
        insertCmd.Parameters.AddWithValue("@ContentType", (object?)contentType ?? DBNull.Value);
        insertCmd.Parameters.AddWithValue("@CreatedBy", createdBy);
        insertCmd.Parameters.AddWithValue("@ModifiedBy", (object?)userId ?? DBNull.Value);
        await insertCmd.ExecuteNonQueryAsync(cancellationToken);

        return attachmentId;
    }

    public async Task ValidateInstanceAsync(Guid workflowId, Guid instanceId, CancellationToken cancellationToken)
    {
        var connectionString = _connectionProvider.ConnectionString
            ?? throw new InvalidOperationException("Tenant connection string not resolved.");

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT 1 FROM workflow.WorkflowInstanceLookup
            WHERE InstanceId = @InstanceId AND WorkflowId = @WorkflowId;
            """;

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@InstanceId", instanceId);
        cmd.Parameters.AddWithValue("@WorkflowId", workflowId);
        var found = await cmd.ExecuteScalarAsync(cancellationToken);
        if (found == null)
            throw new InvalidOperationException($"Workflow instance {instanceId} not found for workflow {workflowId}.");
    }
}
