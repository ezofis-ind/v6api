using MediatR;
using SaaSApp.MultiTenancy;
using SaaSApp.Users.Application.Contracts;
using SaaSApp.Users.Domain.Events;

namespace SaaSApp.Users.Application.Users.EventHandlers;

/// <summary>
/// Handles UserCreatedEvent - enqueues background job for welcome email (Hangfire-ready).
/// </summary>
public sealed class UserCreatedEventHandler : INotificationHandler<UserCreatedEvent>
{
    private readonly IWelcomeEmailJobClient _welcomeEmailJobClient;
    private readonly ITenantProvider _tenantProvider;

    public UserCreatedEventHandler(
        IWelcomeEmailJobClient welcomeEmailJobClient,
        ITenantProvider tenantProvider)
    {
        _welcomeEmailJobClient = welcomeEmailJobClient;
        _tenantProvider = tenantProvider;
    }

    public async Task Handle(UserCreatedEvent notification, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId()
            ?? throw new InvalidOperationException("X-Tenant-Id / tenant context is required to enqueue welcome email.");
        await _welcomeEmailJobClient.EnqueueWelcomeEmailAsync(
            tenantId,
            notification.UserId,
            notification.Email,
            notification.DisplayName,
            cancellationToken);
    }
}
