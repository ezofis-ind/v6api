using MediatR;

namespace SaaSApp.Workflow.Application.Workflows.Commands.SyncWorkflowSteps;

/// <summary>Re-sync workflow.WorkflowSteps (and running step instances) from blob JSON.</summary>
public record SyncWorkflowStepsFromJsonCommand(Guid WorkflowId) : IRequest<SyncWorkflowStepsFromJsonCommandResult>;

public record SyncWorkflowStepsFromJsonCommandResult(bool Found, int StepCount);
