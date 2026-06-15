using MediatR;

namespace SaaSApp.Workflow.Application.Workflows.Commands.PerformAction;

/// <summary>Perform a custom action on a workflow instance (Approve, Reject, Hold, Resume, etc.).</summary>
public record PerformActionCommand(
    Guid WorkflowInstanceId,
    WorkflowAction Action,
    string? Comments = null,
    Guid? AssignToUserId = null,
    bool MoveToNextStep = true
) : IRequest<PerformActionCommandResult>;

/// <summary>Available workflow actions.</summary>
public enum WorkflowAction
{
    Approve = 1,
    Reject = 2,
    Hold = 3,
    Resume = 4,
    Cancel = 5,
    Reassign = 6,
    RequestInfo = 7,
    Complete = 8
}

/// <summary>Result of performing an action.</summary>
public record PerformActionCommandResult(
    bool Success,
    string Message,
    string WorkflowStatus,
    Guid? NextStepInstanceId = null,
    string? NextStepName = null
);
