using MediatR;

namespace SaaSApp.Workflow.Application.Workflows.Commands.ApproveStep;

/// <summary>Approve a workflow step and optionally move to next stage.</summary>
public record ApproveStepCommand(
    Guid WorkflowInstanceId,
    Guid StepInstanceId,
    string? Comments = null,
    bool MoveToNextStep = true
) : IRequest<ApproveStepCommandResult>;

/// <summary>Result of approving a step.</summary>
public record ApproveStepCommandResult(
    bool Success,
    string Message,
    Guid? NextStepInstanceId = null,
    string? CurrentStepName = null,
    string? NextStepName = null
);
