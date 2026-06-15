using Microsoft.EntityFrameworkCore;
using SaaSApp.Workflow.Application.Contracts;
using SaaSApp.Workflow.Domain.Entities;
using SaaSApp.Workflow.Domain.Enums;
using System.Linq;

namespace SaaSApp.Workflow.Infrastructure.Persistence;

public sealed class WorkflowRepository : IWorkflowRepository
{
    private readonly WorkflowDbContext _context;
    private readonly IWorkflowInstanceStore _instanceStore;

    public WorkflowRepository(WorkflowDbContext context, IWorkflowInstanceStore instanceStore)
    {
        _context = context;
        _instanceStore = instanceStore;
    }

    public async Task AddAsync(Domain.Entities.Workflow workflow, CancellationToken cancellationToken = default)
    {
        await _context.Workflows.AddAsync(workflow, cancellationToken);
    }

    public async Task<Domain.Entities.Workflow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Workflows
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == id && !w.IsDeleted, cancellationToken);
    }

    public async Task<Domain.Entities.Workflow?> GetByNameAsync(string name, Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _context.Workflows
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Name == name && w.TenantId == tenantId && !w.IsDeleted, cancellationToken);
    }

    public async Task<Domain.Entities.Workflow?> GetByIdWithStepsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Workflows
            .AsNoTracking()
            .AsSplitQuery()
            .Include(w => w.Steps)
            .Include(w => w.Sla)
            .FirstOrDefaultAsync(w => w.Id == id && !w.IsDeleted, cancellationToken);
    }

    public async Task<IReadOnlyList<Domain.Entities.Workflow>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Workflows
            .AsNoTracking()
            .Where(w => !w.IsDeleted)
            .OrderBy(w => w.Name)
            .ToListAsync(cancellationToken);
    }

    public void Update(Domain.Entities.Workflow workflow)
    {
        _context.Workflows.Update(workflow);
    }

    public async Task AddStepAsync(WorkflowStep step, CancellationToken cancellationToken = default)
    {
        await _context.WorkflowSteps.AddAsync(step, cancellationToken);
    }

    public void Delete(Domain.Entities.Workflow workflow)
    {
        _context.Workflows.Update(workflow);
    }

    public async Task AddInstanceAsync(WorkflowInstance instance, CancellationToken cancellationToken = default)
    {
        await _instanceStore.AddAsync(instance, cancellationToken);
    }

    public async Task<WorkflowInstance?> GetInstanceByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _instanceStore.GetByIdAsync(id, cancellationToken);
    }

    public async Task<IReadOnlyList<WorkflowInstance>> ListInstancesAsync(Guid workflowId, CancellationToken cancellationToken = default)
    {
        return await _instanceStore.ListByWorkflowIdAsync(workflowId, cancellationToken);
    }

    public async Task UpdateInstanceAsync(WorkflowInstance instance, CancellationToken cancellationToken = default)
    {
        await _instanceStore.UpdateAsync(instance, cancellationToken);
    }

    public async Task AddApprovalAsync(WorkflowApproval approval, CancellationToken cancellationToken = default)
    {
        await _context.WorkflowApprovals.AddAsync(approval, cancellationToken);
    }

    public async Task<WorkflowApproval?> GetApprovalByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.WorkflowApprovals.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<WorkflowApproval>> ListPendingApprovalsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.WorkflowApprovals
            .AsNoTracking()
            .Where(a => a.Status == ApprovalStatus.Pending && (a.AssignedToUserId == userId || a.AssignedToUserId == null))
            .OrderBy(a => a.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountApprovedForStepInstanceAsync(Guid stepInstanceId, CancellationToken cancellationToken = default)
    {
        return await _context.WorkflowApprovals
            .CountAsync(a => a.StepInstanceId == stepInstanceId && a.Status == ApprovalStatus.Approved, cancellationToken);
    }

    public void UpdateApproval(WorkflowApproval approval)
    {
        _context.WorkflowApprovals.Update(approval);
    }

    public async Task<IReadOnlyList<SlaBreachInfo>> ListSlaBreachesAsync(CancellationToken cancellationToken = default)
    {
        return await _instanceStore.ListSlaBreachesAsync(cancellationToken);
    }

    public async Task<WorkflowCounts> GetWorkflowCountsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _instanceStore.GetWorkflowCountsAsync(userId, cancellationToken);
    }

    public async Task<(List<WorkflowInstance> Items, int TotalCount)> GetMyInboxAsync(Guid userId, int pageNumber, int pageSize, Guid? workflowId = null, CancellationToken cancellationToken = default)
    {
        return await _instanceStore.GetMyInboxAsync(userId, pageNumber, pageSize, workflowId, cancellationToken);
    }

    public async Task<IReadOnlyList<WorkflowInboxCount>> GetWorkflowWiseInboxCountsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _instanceStore.GetWorkflowWiseInboxCountsAsync(userId, cancellationToken);
    }

    public async Task<(List<WorkflowInstance> Items, int TotalCount)> GetMySentAsync(Guid userId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        return await _instanceStore.GetMySentAsync(userId, pageNumber, pageSize, cancellationToken);
    }

    public async Task<(List<WorkflowInstance> Items, int TotalCount)> GetMyCompletedAsync(Guid userId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        return await _instanceStore.GetMyCompletedAsync(userId, pageNumber, pageSize, cancellationToken);
    }
}
