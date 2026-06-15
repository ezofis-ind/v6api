using MediatR;

namespace SaaSApp.Workflow.Application.Workflows.Commands.RejectStep;

/// <summary>Reject a workflow step and optionally cancel or reassign the workflow.</summary>
public record RejectStepCommand(
    Guid WorkflowInstanceId,
    Guid StepInstanceId,
    string Reason,
    bool CancelWorkflow = false,
    Guid? ReassignToUserId = null
) : IRequest<RejectStepCommandResult>;

/// <summary>Result of rejecting a step.</summary>
public record RejectStepCommandResult(
    bool Success,
    string Message,
    string WorkflowStatus
);
