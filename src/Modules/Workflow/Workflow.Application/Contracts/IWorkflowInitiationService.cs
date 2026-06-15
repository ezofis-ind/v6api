using SaaSApp.Workflow.Application.Workflows.Commands.CreateWorkflow;

namespace SaaSApp.Workflow.Application.Contracts;

/// <summary>Sets up auto-initiation for workflows (Master Form, Email, etc.).</summary>
public interface IWorkflowInitiationService
{
    Task SetupAutoInitiationAsync(
        Guid workflowId,
        List<WorkflowBlockDto> blocks,
        WorkflowInitiateUsingDto initiateUsing,
        List<WorkflowConnectionDto>? rules,
        CancellationToken cancellationToken = default);
}

