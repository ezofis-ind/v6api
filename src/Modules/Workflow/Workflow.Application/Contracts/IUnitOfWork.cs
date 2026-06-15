namespace SaaSApp.Workflow.Application.Contracts;

/// <summary>Unit of work for workflow persistence.</summary>
public interface IUnitOfWork
{
    /// <summary>Save changes to the workflow database.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
