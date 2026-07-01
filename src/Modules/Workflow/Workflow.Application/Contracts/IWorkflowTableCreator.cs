namespace SaaSApp.Workflow.Application.Contracts;

/// <summary>
/// Creates dynamic tables for each workflow.
/// When a workflow is published, creates dedicated tables: Comments_X, Attachments_X, etc.
/// </summary>
public interface IWorkflowTableCreator
{
    Task CreateWorkflowTablesAsync(Guid workflowId, string connectionString, CancellationToken cancellationToken = default);
    Task<bool> WorkflowCoreTablesExistAsync(Guid workflowId, string connectionString, CancellationToken cancellationToken = default);
    Task EnsureWorkflowTablesForStartAsync(Guid workflowId, string connectionString, CancellationToken cancellationToken = default);
    Task EnsureLegacyTransactionTableAsync(Guid workflowId, string connectionString, CancellationToken cancellationToken = default);
    Task EnsureLegacyMailboxTablesAsync(Guid workflowId, string connectionString, CancellationToken cancellationToken = default);
    Task EnsureAgentDataValidationTableAsync(Guid workflowId, string connectionString, CancellationToken cancellationToken = default);
    Task DropWorkflowTablesAsync(Guid workflowId, string connectionString, CancellationToken cancellationToken = default);
}
