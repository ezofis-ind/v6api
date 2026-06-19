using Microsoft.Data.SqlClient;
using SaaSApp.Workflow.Application.Contracts;
using SaaSApp.Workflow.Domain.Enums;

namespace SaaSApp.Workflow.Infrastructure.Services;

/// <summary>
/// Builds instance history from <c>transaction_{workflowSuffix}</c> for the given instance.
/// One transaction row = one timeline entry (start, ap_agent, verified, approved, completed, …).
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
        var profileByUserId = await _userEmails.GetProfilesAsync(userIds, cancellationToken);

        var flows = new List<WorkflowInstanceHistoryFlowDto>();
        var sequence = 0;

        foreach (var tx in transactions)
        {
            sequence++;
            flows.Add(MapTransactionToFlow(sequence, tx, profileByUserId));
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
        IReadOnlyDictionary<Guid, UserProfileLookupDto> profileByUserId)
    {
        var stageLabel = string.IsNullOrWhiteSpace(tx.StageName) ? tx.StageType : tx.StageName;
        var stageDisplay = string.IsNullOrWhiteSpace(stageLabel) ? "Stage" : stageLabel;
        var createdByName = ResolveUserEmail(tx.CreatedBy, profileByUserId);
        var modifiedByName = ResolveUserEmail(tx.ModifiedBy, profileByUserId);

        string action;
        string title;
        string? description;
        DateTime occurredAt;

        if (string.Equals(tx.StageType, EndStageType, StringComparison.OrdinalIgnoreCase))
        {
            action = "complete";
            title = "Completed";
            description = string.IsNullOrWhiteSpace(tx.Review) ? "Workflow finished" : tx.Review;
            occurredAt = tx.ModifiedAt ?? tx.CreatedAt;
        }
        else if (!string.IsNullOrWhiteSpace(tx.Review))
        {
            action = "submit";
            occurredAt = tx.ModifiedAt ?? tx.CreatedAt;
            if (IsApAgentStage(tx))
            {
                title = "AP Agent";
                description = $"Review: {tx.Review}";
            }
            else if (IsVerifiedStage(tx))
            {
                title = "Verified";
                description = $"Review: {tx.Review}";
            }
            else if (IsApprovedReview(tx.Review))
            {
                title = "Approved";
                description = tx.Review;
            }
            else
            {
                title = $"Submitted — {stageDisplay}";
                description = tx.Review;
            }
        }
        else if (tx.ActionStatus == ActionStatusCompleted)
        {
            action = "move";
            title = $"Step completed — {stageDisplay}";
            description = null;
            occurredAt = tx.ModifiedAt ?? tx.CreatedAt;
        }
        else
        {
            action = "move";
            occurredAt = tx.CreatedAt;
            if (sequence == 1)
            {
                title = "Started";
                description = $"Moved to {stageDisplay}";
            }
            else
            {
                title = $"Moved to {stageDisplay}";
                description = tx.ActivityId;
            }
        }

        var milestone = ResolveMilestone(tx, action, sequence);
        var performerId = ResolvePerformerUserId(tx, action);
        var performerName = ResolveUserEmail(performerId, profileByUserId);

        return new WorkflowInstanceHistoryFlowDto(
            sequence,
            tx.Id,
            milestone,
            action,
            title,
            description,
            occurredAt,
            occurredAt,
            performerId,
            performerName,
            stageDisplay,
            tx.StageType,
            tx.ActivityId,
            tx.CreatedBy,
            createdByName,
            tx.ModifiedBy,
            modifiedByName,
            tx.Review,
            tx.ActionStatus);
    }

    private static Guid? ResolvePerformerUserId(TransactionHistoryRow tx, string action) =>
        action is "submit" or "complete"
            ? tx.ModifiedBy ?? tx.CreatedBy
            : tx.CreatedBy;

    private static string ResolveMilestone(TransactionHistoryRow tx, string action, int sequence)
    {
        if (string.Equals(tx.StageType, EndStageType, StringComparison.OrdinalIgnoreCase) || action == "complete")
            return "completed";

        if (IsApAgentStage(tx))
            return "ap_agent";

        if (sequence == 1 && action == "move")
            return "start";

        var stage = (tx.StageName ?? tx.StageType ?? string.Empty).ToLowerInvariant();

        if (stage.Contains("start", StringComparison.Ordinal) && action == "move")
            return "start";

        if (!string.IsNullOrWhiteSpace(tx.Review))
        {
            if (IsVerifiedStage(tx) || stage.Contains("verify", StringComparison.Ordinal))
                return "verified";
            if (IsApprovedReview(tx.Review) || stage.Contains("approv", StringComparison.Ordinal))
                return "approved";
            return "submitted";
        }

        if (stage.Contains("verify", StringComparison.Ordinal) || stage.Contains("verifier", StringComparison.Ordinal))
            return "verified";
        if (stage.Contains("approv", StringComparison.Ordinal))
            return "approved";

        return action == "move" ? "moved" : "submitted";
    }

    private static bool IsApAgentStage(TransactionHistoryRow tx) =>
        string.Equals(tx.StageType, "AP_AGENT", StringComparison.OrdinalIgnoreCase)
        || (tx.StageName?.Contains("ap agent", StringComparison.OrdinalIgnoreCase) ?? false)
        || (tx.StageName?.Contains("apagent", StringComparison.OrdinalIgnoreCase) ?? false);

    private static bool IsVerifiedStage(TransactionHistoryRow tx)
    {
        var stage = (tx.StageName ?? tx.StageType ?? string.Empty);
        return stage.Contains("verify", StringComparison.OrdinalIgnoreCase)
            || stage.Contains("verifier", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsApprovedReview(string review) =>
        review.Contains("approve", StringComparison.OrdinalIgnoreCase)
        || string.Equals(review.Trim(), "APPROVED", StringComparison.OrdinalIgnoreCase);

    private static string? ResolveUserEmail(
        Guid? userId,
        IReadOnlyDictionary<Guid, UserProfileLookupDto> profileByUserId)
    {
        if (userId is not { } id || id == Guid.Empty)
            return null;

        return profileByUserId.TryGetValue(id, out var profile) ? profile.Email : null;
    }

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
