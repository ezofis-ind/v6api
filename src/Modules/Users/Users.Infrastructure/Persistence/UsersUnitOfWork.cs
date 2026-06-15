using SaaSApp.Users.Application.Contracts;
using SaaSApp.Users.Infrastructure.DomainEvents;

namespace SaaSApp.Users.Infrastructure.Persistence;

public sealed class UsersUnitOfWork : IUnitOfWork
{
    private readonly UsersDbContext _context;
    private readonly DomainEventDispatcher _domainEventDispatcher;

    public UsersUnitOfWork(UsersDbContext context, DomainEventDispatcher domainEventDispatcher)
    {
        _context = context;
        _domainEventDispatcher = domainEventDispatcher;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
        await _domainEventDispatcher.DispatchAsync(_context, cancellationToken);
    }
}
