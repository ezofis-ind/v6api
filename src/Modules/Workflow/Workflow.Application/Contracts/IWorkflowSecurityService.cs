using SaaSApp.Workflow.Application.Workflows.Commands.CreateWorkflow;

namespace SaaSApp.Workflow.Application.Contracts;

/// <summary>Manages workflow security and user assignments.</summary>
public interface IWorkflowSecurityService
{
    Task EnsureDefaultWorkflowSecurityAsync(
        Guid workflowId,
        CancellationToken cancellationToken = default);

    Task SetWorkflowSecurityAsync(
        Guid workflowId,
        string[]? coordinators,
        string[]? superusers,
        List<WorkflowBlockDto> blocks,
        CancellationToken cancellationToken = default);

    Task SetWorkflowUsersByDomainAsync(
        Guid workflowId,
        string[] domains,
        CancellationToken cancellationToken = default);
}

