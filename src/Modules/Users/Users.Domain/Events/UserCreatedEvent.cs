using MediatR;
using SaaSApp.SharedKernel.Domain;

namespace SaaSApp.Users.Domain.Events;

public sealed record UserCreatedEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    Guid UserId,
    Guid TenantId,
    string Email,
    string DisplayName) : IDomainEvent, INotification;
