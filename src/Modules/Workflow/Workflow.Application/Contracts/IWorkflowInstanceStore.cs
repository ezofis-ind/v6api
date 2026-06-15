using SaaSApp.Workflow.Domain.Entities;

namespace SaaSApp.Workflow.Application.Contracts;

/// <summary>
/// Persists workflow instances to per-workflow tables (WorkflowInstances_{suffix}, WorkflowStepInstances_{suffix}).
/// Uses WorkflowInstanceLookup for cross-workflow queries (inbox, sent, etc.).
/// </summary>
public interface IWorkflowInstanceStore
{
    Task AddAsync(WorkflowInstance instance, CancellationToken cancellationToken = default);
    Task<WorkflowInstance?> GetByIdAsync(Guid instanceId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkflowInstance>> ListByWorkflowIdAsync(Guid workflowId, CancellationToken cancellationToken = default);
    Task UpdateAsync(WorkflowInstance instance, CancellationToken cancellationToken = default);
    Task<(List<WorkflowInstance> Items, int TotalCount)> GetMyInboxAsync(Guid userId, int pageNumber, int pageSize, Guid? workflowId = null, CancellationToken cancellationToken = default);
    /// <summary>Get inbox count grouped by workflow for current user.</summary>
    Task<IReadOnlyList<WorkflowInboxCount>> GetWorkflowWiseInboxCountsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<(List<WorkflowInstance> Items, int TotalCount)> GetMySentAsync(Guid userId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<(List<WorkflowInstance> Items, int TotalCount)> GetMyCompletedAsync(Guid userId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<WorkflowCounts> GetWorkflowCountsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SlaBreachInfo>> ListSlaBreachesAsync(CancellationToken cancellationToken = default);
}
