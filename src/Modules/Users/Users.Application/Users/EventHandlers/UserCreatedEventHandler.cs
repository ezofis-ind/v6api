using MediatR;
using SaaSApp.Users.Application.Contracts;
using SaaSApp.Users.Domain.Events;

namespace SaaSApp.Users.Application.Users.EventHandlers;

/// <summary>
/// Handles UserCreatedEvent - enqueues background job for welcome email (Hangfire-ready).
/// </summary>
public sealed class UserCreatedEventHandler : INotificationHandler<UserCreatedEvent>
{
    private readonly IWelcomeEmailJobClient _welcomeEmailJobClient;

    public UserCreatedEventHandler(IWelcomeEmailJobClient welcomeEmailJobClient)
    {
        _welcomeEmailJobClient = welcomeEmailJobClient;
    }

    public Task Handle(UserCreatedEvent notification, CancellationToken cancellationToken)
    {
        _welcomeEmailJobClient.EnqueueWelcomeEmail(notification.UserId, notification.Email, notification.DisplayName);
        return Task.CompletedTask;
    }
}
