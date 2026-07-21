namespace SaaSApp.Users.Application.Contracts;

/// <summary>
/// Abstraction for enqueueing welcome email background job (Hangfire implementation in Infrastructure).
/// </summary>
public interface IWelcomeEmailJobClient
{
    Task EnqueueWelcomeEmailAsync(Guid tenantId, Guid userId, string email, string displayName, CancellationToken cancellationToken = default);
}
