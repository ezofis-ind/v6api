using MediatR;

namespace SaaSApp.Workflow.Application.Workflows.Commands.PublishWorkflow;

/// <summary>Publish a workflow (make it active so instances can be started).</summary>
public record PublishWorkflowCommand(Guid WorkflowId) : IRequest<PublishWorkflowCommandResult>;

/// <summary>Whether the workflow was found and published.</summary>
public record PublishWorkflowCommandResult(bool Found);
