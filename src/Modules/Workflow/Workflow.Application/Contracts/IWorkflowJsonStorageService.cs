namespace SaaSApp.Workflow.Application.Contracts;

/// <summary>Stores and retrieves workflow JSON definitions.</summary>
public interface IWorkflowJsonStorageService
{
    Task SaveWorkflowJsonAsync(Guid workflowId, string json, CancellationToken cancellationToken = default);
    Task<string?> GetWorkflowJsonAsync(Guid workflowId, CancellationToken cancellationToken = default);
    Task DeleteWorkflowJsonAsync(Guid workflowId, CancellationToken cancellationToken = default);
}

