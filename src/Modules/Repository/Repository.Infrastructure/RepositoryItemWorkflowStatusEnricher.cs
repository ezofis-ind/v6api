using Microsoft.Data.SqlClient;
using SaaSApp.Repository.Application.Contracts;

namespace SaaSApp.Repository.Infrastructure;

/// <summary>
/// For repository items linked to a workflow ticket (WorkflowInstanceId),
/// replaces Status with the current workflow stage / ticket status for the UI Status column.
/// </summary>
internal static class RepositoryItemWorkflowStatusEnricher
{
    // Matches WorkflowInstanceStatus enum values without taking a Workflow project reference.
    private const int InstancePending = 0;
    private const int InstanceRunning = 1;
    private const int InstancePaused = 2;
    private const int InstanceCompleted = 3;
    private const int InstanceFailed = 4;
    private const int InstanceCancelled = 5;

    public static async Task EnrichListAsync(
        SqlConnection connection,
        IList<RepositoryItemListDto> items,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0)
            return;

        var instanceIds = items
            .Where(i => i.WorkflowInstanceId is Guid id && id != Guid.Empty)
            .Select(i => i.WorkflowInstanceId!.Value)
            .Distinct()
            .ToList();

        if (instanceIds.Count == 0)
            return;

        var statusByInstance = await ResolveStatusesAsync(connection, instanceIds, cancellationToken);
        if (statusByInstance.Count == 0)
            return;

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item.WorkflowInstanceId is not Guid wfId
                || !statusByInstance.TryGetValue(wfId, out var workflowStatus)
                || string.IsNullOrWhiteSpace(workflowStatus))
            {
                continue;
            }

            items[i] = item with { Status = workflowStatus };
        }
    }

    public static async Task<string?> ResolveStatusAsync(
        SqlConnection connection,
        Guid workflowInstanceId,
        CancellationToken cancellationToken)
    {
        var map = await ResolveStatusesAsync(connection, [workflowInstanceId], cancellationToken);
        return map.TryGetValue(workflowInstanceId, out var status) ? status : null;
    }

    /// <summary>
    /// Instance IDs whose <em>display</em> status (same as list enrichment) matches any of
    /// <paramref name="displayStatuses"/> (case-insensitive).
    /// </summary>
    public static async Task<IReadOnlyList<Guid>> FindInstanceIdsWithDisplayStatusAsync(
        SqlConnection connection,
        IReadOnlyList<Guid> candidateInstanceIds,
        IReadOnlyList<string> displayStatuses,
        CancellationToken cancellationToken)
    {
        var wanted = new HashSet<string>(
            displayStatuses
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim()),
            StringComparer.OrdinalIgnoreCase);
        if (wanted.Count == 0 || candidateInstanceIds.Count == 0)
            return Array.Empty<Guid>();

        var statusByInstance = await ResolveStatusesAsync(connection, candidateInstanceIds, cancellationToken);
        if (statusByInstance.Count == 0)
            return Array.Empty<Guid>();

        return statusByInstance
            .Where(kv => wanted.Contains(kv.Value))
            .Select(kv => kv.Key)
            .Distinct()
            .ToList();
    }

    /// <summary>Display Status for each instance (same values as list enrichment).</summary>
    public static Task<Dictionary<Guid, string>> GetDisplayStatusMapAsync(
        SqlConnection connection,
        IReadOnlyList<Guid> candidateInstanceIds,
        CancellationToken cancellationToken) =>
        ResolveStatusesAsync(connection, candidateInstanceIds, cancellationToken);

    private static async Task<Dictionary<Guid, string>> ResolveStatusesAsync(
        SqlConnection connection,
        IReadOnlyList<Guid> instanceIds,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, string>();
        if (instanceIds.Count == 0)
            return result;

        if (!await TableExistsAsync(connection, "WorkflowInstanceLookup", cancellationToken))
            return result;

        var lookups = await LoadLookupsAsync(connection, instanceIds, cancellationToken);
        if (lookups.Count == 0)
            return result;

        foreach (var group in lookups.GroupBy(x => x.WorkflowId))
        {
            var suffix = group.Key.ToString("N")[..8];
            var transactionTable = $"transaction_{suffix}";
            if (!await TableExistsAsync(connection, transactionTable, cancellationToken))
            {
                foreach (var row in group)
                    result[row.InstanceId] = MapInstanceStatus(row.Status);
                continue;
            }

            var groupIds = group.Select(x => x.InstanceId).ToList();
            var stageByInstance = await LoadCurrentStagesAsync(
                connection,
                transactionTable,
                groupIds,
                cancellationToken);

            foreach (var row in group)
            {
                if (stageByInstance.TryGetValue(row.InstanceId, out var stage)
                    && !string.IsNullOrWhiteSpace(stage))
                {
                    result[row.InstanceId] = stage.Trim();
                }
                else
                {
                    result[row.InstanceId] = MapInstanceStatus(row.Status);
                }
            }
        }

        return result;
    }

    private static async Task<List<(Guid InstanceId, Guid WorkflowId, int Status)>> LoadLookupsAsync(
        SqlConnection connection,
        IReadOnlyList<Guid> instanceIds,
        CancellationToken cancellationToken)
    {
        var list = new List<(Guid, Guid, int)>();
        const string sql = """
            SELECT InstanceId, WorkflowId, Status
            FROM workflow.WorkflowInstanceLookup
            WHERE InstanceId IN (SELECT value FROM OPENJSON(@Ids));
            """;

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Ids", ToJsonGuidArray(instanceIds));
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            list.Add((reader.GetGuid(0), reader.GetGuid(1), reader.GetInt32(2)));

        return list;
    }

    private static async Task<Dictionary<Guid, string>> LoadCurrentStagesAsync(
        SqlConnection connection,
        string transactionTableName,
        IReadOnlyList<Guid> instanceIds,
        CancellationToken cancellationToken)
    {
        var map = new Dictionary<Guid, string>();
        var table = $"workflow.[{transactionTableName}]";

        var sql = $"""
            WITH ranked AS (
                SELECT
                    WorkflowInstanceId,
                    StageName,
                    Review,
                    ActionStatus,
                    ROW_NUMBER() OVER (
                        PARTITION BY WorkflowInstanceId
                        ORDER BY
                            CASE WHEN ActionStatus = 0 AND UPPER(LTRIM(RTRIM(ISNULL(StageType, N'')))) <> N'END' THEN 0 ELSE 1 END,
                            Id DESC
                    ) AS rn
                FROM {table}
                WHERE IsDeleted = 0
                  AND WorkflowInstanceId IN (SELECT value FROM OPENJSON(@Ids))
            )
            SELECT
                WorkflowInstanceId,
                CASE
                    WHEN ActionStatus = 0 AND NULLIF(LTRIM(RTRIM(StageName)), N'') IS NOT NULL THEN StageName
                    WHEN NULLIF(LTRIM(RTRIM(Review)), N'') IS NOT NULL
                         AND UPPER(LTRIM(RTRIM(Review))) <> N'END' THEN Review
                    WHEN NULLIF(LTRIM(RTRIM(StageName)), N'') IS NOT NULL THEN StageName
                    ELSE NULL
                END AS DisplayStatus
            FROM ranked
            WHERE rn = 1;
            """;

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Ids", ToJsonGuidArray(instanceIds));
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (reader.IsDBNull(1))
                continue;
            var status = reader.GetString(1);
            if (!string.IsNullOrWhiteSpace(status))
                map[reader.GetGuid(0)] = status.Trim();
        }

        return map;
    }

    private static string MapInstanceStatus(int status) =>
        status switch
        {
            InstancePending => "Pending",
            InstanceRunning => "Pending Approval",
            InstancePaused => "Paused",
            InstanceCompleted => "Approved",
            InstanceFailed => "Failed",
            InstanceCancelled => "Cancelled",
            _ => "Active"
        };

    private static string ToJsonGuidArray(IReadOnlyList<Guid> ids) =>
        "[" + string.Join(",", ids.Select(id => $"\"{id:D}\"")) + "]";

    private static async Task<bool> TableExistsAsync(
        SqlConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT 1 FROM sys.tables t
            INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
            WHERE t.name = @Name AND s.name = N'workflow';
            """;
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Name", tableName);
        return await cmd.ExecuteScalarAsync(cancellationToken) is not null;
    }
}
