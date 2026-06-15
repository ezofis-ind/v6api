using MediatR;
using SaaSApp.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace SaaSApp.Users.Infrastructure.DomainEvents;

/// <summary>
/// Dispatches domain events from entities to MediatR handlers (e.g. UserCreatedEvent -> SendWelcomeEmailHandler).
/// Call after SaveChangesAsync to publish events from the current context's entities.
/// </summary>
public sealed class DomainEventDispatcher
{
    private readonly IMediator _mediator;

    public DomainEventDispatcher(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task DispatchAsync(DbContext context, CancellationToken cancellationToken = default)
    {
        var domainEvents = context.ChangeTracker
            .Entries<Entity>()
            .SelectMany(e => e.Entity.DomainEvents)
            .ToList();

        foreach (var entry in context.ChangeTracker.Entries<Entity>())
            entry.Entity.ClearDomainEvents();

        foreach (var domainEvent in domainEvents)
        {
            await _mediator.Publish(domainEvent, cancellationToken);
        }
    }
}
