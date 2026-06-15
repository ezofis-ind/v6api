namespace SaaSApp.Workflow.Application.Contracts;

public interface IWorkflowInstanceHistoryService
{
    Task<WorkflowInstanceHistoryResult?> GetHistoryAsync(
        Guid workflowId,
        Guid instanceId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Instance timeline from <c>workflow.transaction_{suffix}</c> only (one row per transaction = one flow step).
/// </summary>
public sealed record WorkflowInstanceHistoryResult(
    Guid WorkflowId,
    Guid InstanceId,
    string WorkflowName,
    string? ReferenceNumber,
    int Status,
    string StatusName,
    int FlowCount,
    IReadOnlyList<WorkflowInstanceHistoryFlowDto> Flows);

/// <summary>
/// One flow step from a transaction row. <see cref="Action"/>: move (opened stage), submit (review), complete (END).
/// </summary>
public sealed record WorkflowInstanceHistoryFlowDto(
    int Sequence,
    int TransactionRowId,
    string Action,
    string Title,
    string? Description,
    DateTime OccurredAtUtc,
    string? StageName,
    string? StageType,
    string? ActivityId,
    Guid? CreatedBy,
    string? CreatedByName,
    Guid? ModifiedBy,
    string? ModifiedByName,
    string? Review,
    int ActionStatus);
