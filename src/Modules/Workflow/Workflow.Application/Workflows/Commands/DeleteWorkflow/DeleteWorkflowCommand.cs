using MediatR;

namespace SaaSApp.Workflow.Application.Workflows.Commands.DeleteWorkflow;

/// <summary>Soft-delete a workflow by ID.</summary>
public record DeleteWorkflowCommand(Guid WorkflowId) : IRequest<DeleteWorkflowCommandResult>;

/// <summary>Whether the workflow was found and soft-deleted.</summary>
public record DeleteWorkflowCommandResult(bool Found);
