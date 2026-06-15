using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SaaSApp.Workflow.Application.Workflows;
using SaaSApp.Workflow.Application.Contracts;
using SaaSApp.Workflow.Application.Workflows.Commands.CreateWorkflow;
using SaaSApp.Workflow.Domain.Entities;
using SaaSApp.Workflow.Domain.Enums;
using SaaSApp.Workflow.Infrastructure.Persistence;

namespace SaaSApp.Workflow.Infrastructure.Services;

/// <summary>
/// After POST/PUT /api/workflows saves designer JSON, populates workflow.WorkflowSteps
/// with ActivityId (block Id), StageType (block Type), and assignees from Settings.Users.
/// </summary>
public sealed class WorkflowStepSyncService : IWorkflowStepSyncService
{
    private readonly WorkflowDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IWorkflowJsonStorageService _jsonStorage;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<WorkflowStepSyncService> _logger;

    public WorkflowStepSyncService(
        WorkflowDbContext context,
        IUnitOfWork unitOfWork,
        IWorkflowJsonStorageService jsonStorage,
        ITenantContext tenantContext,
        ILogger<WorkflowStepSyncService> logger)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _jsonStorage = jsonStorage;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task SyncStepsFromWorkflowJsonAsync(
        Guid workflowId,
        WorkflowJsonDto? workflowJson = null,
        CancellationToken cancellationToken = default)
    {
        if (workflowJson?.Blocks == null || workflowJson.Blocks.Count == 0)
            workflowJson = await LoadWorkflowJsonAsync(workflowId, cancellationToken);

        if (workflowJson?.Blocks == null || workflowJson.Blocks.Count == 0)
        {
            _logger.LogWarning("No blocks to sync for workflow {WorkflowId} (body and blob empty)", workflowId);
            return;
        }

        await EnsureWorkflowStepsColumnsAsync(cancellationToken);

        var orderedBlocks = OrderBlocksByFlow(workflowJson.Blocks, workflowJson.Rules);
        var deleted = await _context.WorkflowSteps
            .Where(s => s.WorkflowId == workflowId)
            .ExecuteDeleteAsync(cancellationToken);

        var order = 1;
        foreach (var block in orderedBlocks)
        {
            var label = block.Settings?.Label;
            if (string.IsNullOrWhiteSpace(label))
                label = block.Type;

            var (assignedToUserId, approversJson) = ResolveAssignees(block.Settings?.Users);
            var actionsJson = BuildActionsJsonForBlock(block.Id, workflowJson.Rules);
            var step = WorkflowStep.Create(
                workflowId,
                label!,
                MapBlockTypeToStepType(block.Type),
                order++,
                description: null,
                config: null,
                isRequired: !string.Equals(block.Type, "END", StringComparison.OrdinalIgnoreCase),
                assignedToUserId: assignedToUserId,
                assignedToRole: null,
                approversJson: approversJson,
                activityId: block.Id,
                stageType: block.Type,
                actionsJson: actionsJson);

            await _context.WorkflowSteps.AddAsync(step, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Synced {Count} workflow steps from designer JSON for workflow {WorkflowId} (replaced {Deleted})",
            orderedBlocks.Count,
            workflowId,
            deleted);

        await RefreshRunningInstanceStepsFromDefinitionAsync(workflowId, cancellationToken);
    }

    public async Task RefreshRunningInstanceStepsFromDefinitionAsync(
        Guid workflowId,
        CancellationToken cancellationToken = default)
    {
        var connectionString = _tenantContext.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        var suffix = workflowId.ToString("N")[..8];
        var stepInstancesTable = $"workflow.WorkflowStepInstances_{suffix}";
        if (!await TableExistsAsync(connectionString, stepInstancesTable, cancellationToken))
            return;

        var definitionSteps = await _context.WorkflowSteps
            .AsNoTracking()
            .Where(s => s.WorkflowId == workflowId)
            .OrderBy(s => s.Order)
            .ToListAsync(cancellationToken);

        if (definitionSteps.Count == 0)
            return;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        const string instanceIdsSql = @"
SELECT InstanceId
FROM workflow.WorkflowInstanceLookup
WHERE WorkflowId = @WorkflowId AND Status IN (0, 1) AND IsArchived = 0";

        var instanceIds = new List<Guid>();
        await using (var listCmd = new SqlCommand(instanceIdsSql, connection))
        {
            listCmd.Parameters.AddWithValue("@WorkflowId", workflowId);
            await using var reader = await listCmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                instanceIds.Add(reader.GetGuid(0));
        }

        foreach (var instanceId in instanceIds)
        {
            var updateSql = $@"
;WITH def AS (
    SELECT Id, Name, [Order], ActivityId, StageType, AssignedToUserId
    FROM workflow.WorkflowSteps
    WHERE WorkflowId = @WorkflowId
),
inst AS (
    SELECT Id, [Order]
    FROM {stepInstancesTable}
    WHERE WorkflowInstanceId = @InstanceId
)
UPDATE si SET
    si.WorkflowStepId = d.Id,
    si.StepName = d.Name,
    si.ActivityId = d.ActivityId,
    si.StageType = d.StageType,
    si.AssignedToUserId = d.AssignedToUserId
FROM {stepInstancesTable} si
INNER JOIN inst i ON i.Id = si.Id
INNER JOIN def d ON d.[Order] = i.[Order]
WHERE si.WorkflowInstanceId = @InstanceId
  AND (SELECT COUNT(*) FROM def) = (SELECT COUNT(*) FROM inst)";

            await using var cmd = new SqlCommand(updateSql, connection);
            cmd.Parameters.AddWithValue("@WorkflowId", workflowId);
            cmd.Parameters.AddWithValue("@InstanceId", instanceId);
            var rows = await cmd.ExecuteNonQueryAsync(cancellationToken);
            if (rows > 0)
            {
                _logger.LogInformation(
                    "Refreshed {Rows} step instance row(s) for instance {InstanceId} workflow {WorkflowId}",
                    rows,
                    instanceId,
                    workflowId);
            }
        }
    }

    private static async Task<bool> TableExistsAsync(
        string connectionString,
        string tableName,
        CancellationToken cancellationToken)
    {
        var schema = "workflow";
        var name = tableName.Replace("workflow.", "", StringComparison.OrdinalIgnoreCase);
        const string sql = @"
SELECT 1 FROM sys.tables t
INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
WHERE s.name = @Schema AND t.name = @Name";

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Schema", schema);
        cmd.Parameters.AddWithValue("@Name", name);
        return await cmd.ExecuteScalarAsync(cancellationToken) != null;
    }

    private async Task<WorkflowJsonDto?> LoadWorkflowJsonAsync(Guid workflowId, CancellationToken cancellationToken)
    {
        var raw = await _jsonStorage.GetWorkflowJsonAsync(workflowId, cancellationToken);
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        return JsonSerializer.Deserialize<WorkflowJsonDto>(
            raw,
            SaaSApp.Workflow.Application.Workflows.WorkflowJsonSerializerOptions.Deserialize);
    }

    private async Task EnsureWorkflowStepsColumnsAsync(CancellationToken cancellationToken)
    {
        var connectionString = _tenantContext.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        const string sql = @"
IF OBJECT_ID(N'workflow.WorkflowSteps', N'U') IS NOT NULL
  AND NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'workflow.WorkflowSteps') AND name = N'StageType')
BEGIN
    ALTER TABLE workflow.WorkflowSteps ADD StageType NVARCHAR(64) NULL;
END
IF OBJECT_ID(N'workflow.WorkflowSteps', N'U') IS NOT NULL
  AND NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'workflow.WorkflowSteps') AND name = N'ActivityId')
BEGIN
    ALTER TABLE workflow.WorkflowSteps ADD ActivityId NVARCHAR(128) NULL;
END
IF OBJECT_ID(N'workflow.WorkflowSteps', N'U') IS NOT NULL
  AND NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'workflow.WorkflowSteps') AND name = N'ActionsJson')
BEGIN
    ALTER TABLE workflow.WorkflowSteps ADD ActionsJson NVARCHAR(MAX) NULL;
END";

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    internal static string? BuildActionsJsonForBlock(string blockId, List<WorkflowConnectionDto>? rules)
    {
        if (rules == null || rules.Count == 0)
            return null;

        var actions = rules
            .Where(r => string.Equals(r.FromBlockId, blockId, StringComparison.OrdinalIgnoreCase))
            .Select(r => new WorkflowStepActionDto(r.Id, r.ProceedAction, r.ToBlockId))
            .ToList();

        return WorkflowStepActionsHelper.SerializeActions(actions);
    }

    internal static List<WorkflowBlockDto> OrderBlocksByFlow(
        List<WorkflowBlockDto> blocks,
        List<WorkflowConnectionDto>? rules)
    {
        var blockById = blocks.ToDictionary(b => b.Id, StringComparer.OrdinalIgnoreCase);
        var start = blocks.FirstOrDefault(b => string.Equals(b.Type, "START", StringComparison.OrdinalIgnoreCase));
        var ordered = new List<WorkflowBlockDto>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (start != null)
        {
            var queue = new Queue<string>();
            queue.Enqueue(start.Id);
            visited.Add(start.Id);

            while (queue.Count > 0)
            {
                var currentId = queue.Dequeue();
                if (!blockById.TryGetValue(currentId, out var current))
                    continue;

                ordered.Add(current);

                if (rules == null)
                    continue;

                foreach (var nextId in rules
                             .Where(r => string.Equals(r.FromBlockId, currentId, StringComparison.OrdinalIgnoreCase))
                             .OrderBy(r => r.Top ?? 0)
                             .ThenBy(r => r.Left ?? 0)
                             .Select(r => r.ToBlockId))
                {
                    if (visited.Add(nextId))
                        queue.Enqueue(nextId);
                }
            }
        }

        foreach (var block in blocks
                     .Where(b => !visited.Contains(b.Id))
                     .OrderBy(b => b.Top ?? 0)
                     .ThenBy(b => b.Left ?? 0))
        {
            ordered.Add(block);
        }

        return ordered;
    }

    internal static StepType MapBlockTypeToStepType(string blockType) =>
        blockType.ToUpperInvariant() switch
        {
            "INTERNAL_ACTOR" or "APPROVAL" or "EXTERNAL_ACTOR" => StepType.Approval,
            "AP_AGENT" or "OCR" or "AUTOMATION" or "API" => StepType.Automated,
            "RULES" or "CONDITION" => StepType.Condition,
            "NOTIFICATION" or "EMAIL" => StepType.Notification,
            "WAIT" or "WAIT_FOR_EVENT" => StepType.WaitForEvent,
            _ => StepType.Manual
        };

    internal static (Guid? AssignedToUserId, string? ApproversJson) ResolveAssignees(string[]? users)
    {
        if (users == null || users.Length == 0)
            return (null, null);

        var parsed = new List<Guid>();
        foreach (var user in users)
        {
            if (Guid.TryParse(user, out var guid))
                parsed.Add(guid);
        }

        if (parsed.Count == 0)
            return (null, users.Length > 0 ? JsonSerializer.Serialize(users) : null);

        var approversJson = JsonSerializer.Serialize(users);
        return (parsed[0], approversJson);
    }
}
