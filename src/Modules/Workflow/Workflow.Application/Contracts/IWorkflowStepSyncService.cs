using SaaSApp.Workflow.Application.Workflows.Commands.CreateWorkflow;

namespace SaaSApp.Workflow.Application.Contracts;

/// <summary>
/// Syncs designer workflow JSON blocks into workflow.WorkflowSteps
/// (ActivityId, StageType, assignees) in flow order.
/// </summary>
public interface IWorkflowStepSyncService
{
    /// <summary>
    /// Replace workflow steps from designer JSON (blocks + rules).
    /// When <paramref name="workflowJson"/> is null, loads JSON from blob storage.
    /// </summary>
    Task SyncStepsFromWorkflowJsonAsync(
        Guid workflowId,
        WorkflowJsonDto? workflowJson = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// After definition sync, copy ActivityId/StageType/assignees onto running instances'
    /// WorkflowStepInstances rows (matched by step order).
    /// </summary>
    Task RefreshRunningInstanceStepsFromDefinitionAsync(
        Guid workflowId,
        CancellationToken cancellationToken = default);
}
