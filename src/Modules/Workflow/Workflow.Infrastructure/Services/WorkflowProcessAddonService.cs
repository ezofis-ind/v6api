using Microsoft.Data.SqlClient;
using SaaSApp.Workflow.Application.Contracts;

namespace SaaSApp.Workflow.Infrastructure.Services;

public sealed class WorkflowProcessAddonService : IWorkflowProcessAddonService
{
    private readonly ITenantContext _tenantContext;
    private readonly IWorkflowTableCreator _tableCreator;

    public WorkflowProcessAddonService(ITenantContext tenantContext, IWorkflowTableCreator tableCreator)
    {
        _tenantContext = tenantContext;
        _tableCreator = tableCreator;
    }

    public async Task<int> InsertAsync(
        Guid workflowId,
        Guid processId,
        Guid repositoryId,
        Guid itemId,
        string? fileName,
        int? transactionId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var connectionString = _tenantContext.ConnectionString
            ?? throw new InvalidOperationException("Tenant connection string not resolved.");

        await _tableCreator.CreateWorkflowTablesAsync(workflowId, connectionString, cancellationToken);

        var suffix = workflowId.ToString("N")[..8];
        var table = $"workflow.[processAddon_{suffix}]";

        var sql = $"""
            INSERT INTO {table}
                (ProcessId, RepositoryId, ItemId, FileName, TransactionId, CreatedAt, CreatedBy, IsDeleted)
            OUTPUT INSERTED.Id
            VALUES
                (@ProcessId, @RepositoryId, @ItemId, @FileName, @TransactionId, SYSUTCDATETIME(), @CreatedBy, 0);
            """;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@ProcessId", processId);
        cmd.Parameters.AddWithValue("@RepositoryId", repositoryId);
        cmd.Parameters.AddWithValue("@ItemId", itemId);
        cmd.Parameters.AddWithValue("@FileName", (object?)fileName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@TransactionId", (object?)transactionId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CreatedBy", userId);

        return Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken));
    }

    public async Task<IReadOnlyList<WorkflowProcessAddonRow>> ListByProcessAsync(
        Guid workflowId,
        Guid processId,
        CancellationToken cancellationToken = default)
    {
        var connectionString = _tenantContext.ConnectionString
            ?? throw new InvalidOperationException("Tenant connection string not resolved.");
        var suffix = workflowId.ToString("N")[..8];
        var table = $"processAddon_{suffix}";

        if (!await TableExistsAsync(connectionString, table, cancellationToken))
            return Array.Empty<WorkflowProcessAddonRow>();

        var sql = $"""
            SELECT Id, ProcessId, RepositoryId, ItemId, FileName, TransactionId, CreatedAt, CreatedBy
            FROM workflow.[{table}]
            WHERE ProcessId = @ProcessId AND IsDeleted = 0
            ORDER BY CreatedAt DESC, Id DESC;
            """;

        var rows = new List<WorkflowProcessAddonRow>();
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@ProcessId", processId);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new WorkflowProcessAddonRow(
                reader.GetInt32(0),
                reader.GetGuid(1),
                reader.GetGuid(2),
                reader.GetGuid(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetInt32(5),
                reader.GetDateTime(6),
                reader.GetGuid(7)));
        }

        return rows;
    }

    private static async Task<bool> TableExistsAsync(
        string connectionString,
        string tableName,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT 1
            FROM sys.tables t
            INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
            WHERE s.name = N'workflow' AND t.name = @TableName;
            """;
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@TableName", tableName);
        return await cmd.ExecuteScalarAsync(cancellationToken) != null;
    }
}
