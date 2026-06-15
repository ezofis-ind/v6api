using SaaSApp.Workflow.Application.Contracts;

namespace SaaSApp.Workflow.Infrastructure.Persistence;

public sealed class WorkflowUnitOfWork : IUnitOfWork
{
    private readonly WorkflowDbContext _context;

    public WorkflowUnitOfWork(WorkflowDbContext context)
    {
        _context = context;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
}
