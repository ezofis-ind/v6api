using Microsoft.Data.SqlClient;
using SaaSApp.Workflow.Application.Contracts;
using SaaSApp.Workflow.Domain.Enums;

namespace SaaSApp.Workflow.Infrastructure.Services;

/// <summary>
/// Builds instance history only from <c>transaction_{workflowSuffix}</c> for the given <see cref="Guid"/> instance.
/// One transaction row = one flow entry (move / submit / complete).
/// </summary>
public sealed class WorkflowInstanceHistoryService : IWorkflowInstanceHistoryService
{
    private const string EndStageType = "END";
    private const int ActionStatusOpen = 0;
    private const int ActionStatusCompleted = 1;

    private readonly ITenantContext _tenantContext;
    private readonly IUserEmailLookup _userEmails;

    public WorkflowInstanceHistoryService(
        ITenantContext tenantContext,
        IUserEmailLookup userEmails)
    {
        _tenantContext = tenantContext;
        _userEmails = userEmails;
    }

    public async Task<WorkflowInstanceHistoryResult?> GetHistoryAsync(
        Guid workflowId,
        Guid instanceId,
        CancellationToken cancellationToken = default)
    {
        var connectionString = _tenantContext.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Tenant connection string not resolved.");

        var suffix = workflowId.ToString("N")[..8];
        var instancesTable = $"workflow.[WorkflowInstances_{suffix}]";
        var transactionTable = $"workflow.[transaction_{suffix}]";

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var instance = await LoadInstanceHeaderAsync(
            connection, instancesTable, suffix, workflowId, instanceId, cancellationToken);
        if (instance == null)
            return null;

        var transactions = await TableExistsAsync(connection, $"transaction_{suffix}", cancellationToken)
            ? await LoadTransactionsAsync(connection, transactionTable, instanceId, cancellationToken)
            : Array.Empty<TransactionHistoryRow>();

        var userIds = transactions
            .SelectMany(t => new[] { t.CreatedBy, t.ModifiedBy })
            .Where(id => id is { } g && g != Guid.Empty)
            .Cast<Guid>();
        var emailByUserId = await _userEmails.GetEmailsAsync(userIds, cancellationToken);

        var flows = new List<WorkflowInstanceHistoryFlowDto>();
        var sequence = 0;

        foreach (var tx in transactions)
        {
            sequence++;
            flows.Add(MapTransactionToFlow(sequence, tx, emailByUserId));
        }

        return new WorkflowInstanceHistoryResult(
            workflowId,
            instanceId,
            instance.WorkflowName,
            instance.ReferenceNumber,
            instance.Status,
            ((WorkflowInstanceStatus)instance.Status).ToString(),
            flows.Count,
            flows);
    }

    private static WorkflowInstanceHistoryFlowDto MapTransactionToFlow(
        int sequence,
        TransactionHistoryRow tx,
        IReadOnlyDictionary<Guid, string> emailByUserId)
    {
        var stageLabel = string.IsNullOrWhiteSpace(tx.StageName) ? tx.StageType : tx.StageName;
        var stageDisplay = string.IsNullOrWhiteSpace(stageLabel) ? "Stage" : stageLabel;
        var createdByName = ResolveUserName(tx.CreatedBy, emailByUserId);
        var modifiedByName = ResolveUserName(tx.ModifiedBy, emailByUserId);

        if (string.Equals(tx.StageType, EndStageType, StringComparison.OrdinalIgnoreCase))
        {
            return BuildFlow(
                sequence, tx, "complete", "Process completed", tx.Review,
                tx.ModifiedAt ?? tx.CreatedAt, stageDisplay, createdByName, modifiedByName);
        }

        if (!string.IsNullOrWhiteSpace(tx.Review))
        {
            var isApAgent = IsApAgentStage(tx);
            return BuildFlow(
                sequence, tx, "submit",
                isApAgent ? $"AP Agent submitted: {tx.Review}" : $"Submitted: {tx.Review}",
                stageDisplay, tx.ModifiedAt ?? tx.CreatedAt, stageDisplay, createdByName, modifiedByName);
        }

        if (tx.ActionStatus == ActionStatusCompleted)
        {
            return BuildFlow(
                sequence, tx, "move", $"Moved — completed: {stageDisplay}", null,
                tx.ModifiedAt ?? tx.CreatedAt, stageDisplay, createdByName, modifiedByName);
        }

        return BuildFlow(
            sequence, tx, "move",
            sequence == 1 ? $"Started — moved to: {stageDisplay}" : $"Moved to: {stageDisplay}",
            tx.ActivityId, tx.CreatedAt, stageDisplay, createdByName, modifiedByName);
    }

    private static WorkflowInstanceHistoryFlowDto BuildFlow(
        int sequence,
        TransactionHistoryRow tx,
        string action,
        string title,
        string? description,
        DateTime occurredAtUtc,
        string stageDisplay,
        string? createdByName,
        string? modifiedByName) =>
        new(
            sequence,
            tx.Id,
            action,
            title,
            description,
            occurredAtUtc,
            stageDisplay,
            tx.StageType,
            tx.ActivityId,
            tx.CreatedBy,
            createdByName,
            tx.ModifiedBy,
            modifiedByName,
            tx.Review,
            tx.ActionStatus);

    private static string? ResolveUserName(Guid? userId, IReadOnlyDictionary<Guid, string> emailByUserId)
    {
        if (userId is not { } id || id == Guid.Empty)
            return null;

        return emailByUserId.TryGetValue(id, out var email) ? email : null;
    }

    private static bool IsApAgentStage(TransactionHistoryRow tx) =>
        string.Equals(tx.StageType, "AP_AGENT", StringComparison.OrdinalIgnoreCase)
        || (tx.StageName?.Contains("ap agent", StringComparison.OrdinalIgnoreCase) ?? false)
        || (tx.StageName?.Contains("apagent", StringComparison.OrdinalIgnoreCase) ?? false);

    private static async Task<InstanceHeaderRow?> LoadInstanceHeaderAsync(
        SqlConnection connection,
        string instancesTable,
        string suffix,
        Guid workflowId,
        Guid instanceId,
        CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(connection, $"WorkflowInstances_{suffix}", cancellationToken))
            return null;

        var sql = $"""
            SELECT TOP 1 WorkflowName, ReferenceNumber, Status
            FROM {instancesTable}
            WHERE Id = @InstanceId AND WorkflowId = @WorkflowId;
            """;

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@InstanceId", instanceId);
        cmd.Parameters.AddWithValue("@WorkflowId", workflowId);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new InstanceHeaderRow(
            reader.GetString(0),
            reader.IsDBNull(1) ? null : reader.GetString(1),
            reader.GetInt32(2));
    }

    private static async Task<IReadOnlyList<TransactionHistoryRow>> LoadTransactionsAsync(
        SqlConnection connection,
        string transactionTable,
        Guid instanceId,
        CancellationToken cancellationToken)
    {
        var sql = $"""
            SELECT
                Id,
                ActivityId,
                StageType,
                StageName,
                Review,
                ActionStatus,
                CreatedAt,
                ModifiedAt,
                CreatedBy,
                ModifiedBy
            FROM {transactionTable}
            WHERE WorkflowInstanceId = @InstanceId
              AND IsDeleted = 0
            ORDER BY CreatedAt ASC, Id ASC;
            """;

        var list = new List<TransactionHistoryRow>();
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@InstanceId", instanceId);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(new TransactionHistoryRow(
                reader.GetInt32(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.GetInt32(5),
                reader.GetDateTime(6),
                reader.IsDBNull(7) ? null : reader.GetDateTime(7),
                reader.IsDBNull(8) ? null : reader.GetGuid(8),
                reader.IsDBNull(9) ? null : reader.GetGuid(9)));
        }

        return list;
    }

    private static async Task<bool> TableExistsAsync(
        SqlConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT 1
            FROM sys.tables t
            INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
            WHERE s.name = N'workflow' AND t.name = @TableName;
            """;

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@TableName", tableName);
        return await cmd.ExecuteScalarAsync(cancellationToken) != null;
    }

    private sealed record InstanceHeaderRow(string WorkflowName, string? ReferenceNumber, int Status);

    private sealed record TransactionHistoryRow(
        int Id,
        string? ActivityId,
        string? StageType,
        string? StageName,
        string? Review,
        int ActionStatus,
        DateTime CreatedAt,
        DateTime? ModifiedAt,
        Guid? CreatedBy,
        Guid? ModifiedBy);
}
