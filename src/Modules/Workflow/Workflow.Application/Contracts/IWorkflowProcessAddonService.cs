namespace SaaSApp.Workflow.Application.Contracts;

/// <summary>Legacy workflow.processAddon_{suffix}: links process (instance) to repository item.</summary>
public interface IWorkflowProcessAddonService
{
    Task<int> InsertAsync(
        Guid workflowId,
        Guid processId,
        Guid repositoryId,
        Guid itemId,
        string? fileName,
        int? transactionId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkflowProcessAddonRow>> ListByProcessAsync(
        Guid workflowId,
        Guid processId,
        CancellationToken cancellationToken = default);
}

public sealed record WorkflowProcessAddonRow(
    int Id,
    Guid ProcessId,
    Guid RepositoryId,
    Guid ItemId,
    string? FileName,
    int? TransactionId,
    DateTime CreatedAt,
    Guid CreatedBy);
