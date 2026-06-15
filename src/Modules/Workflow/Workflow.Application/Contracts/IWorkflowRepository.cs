using SaaSApp.Workflow.Domain.Entities;

namespace SaaSApp.Workflow.Application.Contracts;

/// <summary>Workflow persistence for the current tenant.</summary>
public interface IWorkflowRepository
{
    /// <summary>Add a new workflow.</summary>
    Task AddAsync(Domain.Entities.Workflow workflow, CancellationToken cancellationToken = default);

    /// <summary>Get workflow by ID.</summary>
    Task<Domain.Entities.Workflow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Get workflow by name (for uniqueness validation).</summary>
    Task<Domain.Entities.Workflow?> GetByNameAsync(string name, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Get workflow with steps.</summary>
    Task<Domain.Entities.Workflow?> GetByIdWithStepsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>List all workflows (excluding soft-deleted) ordered by Name.</summary>
    Task<IReadOnlyList<Domain.Entities.Workflow>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Mark workflow as modified.</summary>
    void Update(Domain.Entities.Workflow workflow);

    /// <summary>Add a workflow step.</summary>
    Task AddStepAsync(WorkflowStep step, CancellationToken cancellationToken = default);

    /// <summary>Soft-delete workflow.</summary>
    void Delete(Domain.Entities.Workflow workflow);

    /// <summary>Add workflow instance.</summary>
    Task AddInstanceAsync(WorkflowInstance instance, CancellationToken cancellationToken = default);

    /// <summary>Get workflow instance by ID with step instances.</summary>
    Task<WorkflowInstance?> GetInstanceByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>List workflow instances for a workflow.</summary>
    Task<IReadOnlyList<WorkflowInstance>> ListInstancesAsync(Guid workflowId, CancellationToken cancellationToken = default);

    /// <summary>Persist workflow instance changes (per-workflow tables).</summary>
    Task UpdateInstanceAsync(WorkflowInstance instance, CancellationToken cancellationToken = default);

    /// <summary>Add approval request.</summary>
    Task AddApprovalAsync(WorkflowApproval approval, CancellationToken cancellationToken = default);

    /// <summary>Get approval by ID.</summary>
    Task<WorkflowApproval?> GetApprovalByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>List pending approvals for a user.</summary>
    Task<IReadOnlyList<WorkflowApproval>> ListPendingApprovalsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Count approved records for a step instance (for AllMustApprove policy).</summary>
    Task<int> CountApprovedForStepInstanceAsync(Guid stepInstanceId, CancellationToken cancellationToken = default);

    /// <summary>Mark approval as modified.</summary>
    void UpdateApproval(WorkflowApproval approval);

    /// <summary>List workflow instances with SLA breaches or at-risk status.</summary>
    Task<IReadOnlyList<SlaBreachInfo>> ListSlaBreachesAsync(CancellationToken cancellationToken = default);

    /// <summary>Get workflow counts for current user (Inbox, Sent, Completed).</summary>
    Task<WorkflowCounts> GetWorkflowCountsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Get my inbox (workflows assigned to me). Optional workflowId to filter by workflow.</summary>
    Task<(List<WorkflowInstance> Items, int TotalCount)> GetMyInboxAsync(Guid userId, int pageNumber, int pageSize, Guid? workflowId = null, CancellationToken cancellationToken = default);
    /// <summary>Get inbox count grouped by workflow for current user.</summary>
    Task<IReadOnlyList<WorkflowInboxCount>> GetWorkflowWiseInboxCountsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Get my sent (workflows created by me).</summary>
    Task<(List<WorkflowInstance> Items, int TotalCount)> GetMySentAsync(Guid userId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>Get my completed workflows.</summary>
    Task<(List<WorkflowInstance> Items, int TotalCount)> GetMyCompletedAsync(Guid userId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
}

/// <summary>SLA breach information for reporting.</summary>
public record SlaBreachInfo(
    Guid InstanceId,
    Guid WorkflowId,
    string WorkflowName,
    Domain.Enums.WorkflowInstanceStatus InstanceStatus,
    Domain.Enums.SlaPriority Priority,
    Domain.Enums.SlaStatus ResponseStatus,
    Domain.Enums.SlaStatus ResolutionStatus,
    DateTime ResponseDeadline,
    DateTime ResolutionDeadline,
    bool IsEscalated,
    DateTime CreatedAtUtc);

/// <summary>Inbox count for a single workflow.</summary>
public record WorkflowInboxCount(Guid WorkflowId, string WorkflowName, int InboxCount);

/// <summary>Workflow counts for dashboard.</summary>
public record WorkflowCounts(
    int InboxCount,
    int SentCount,
    int CompletedCount,
    int TotalActive
);
