namespace SaaSApp.Users.Application.Contracts;

/// <summary>
/// Unit of work for the Users module: persists changes and dispatches domain events.
/// </summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
