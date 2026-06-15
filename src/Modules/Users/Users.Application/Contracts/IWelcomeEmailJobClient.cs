namespace SaaSApp.Users.Application.Contracts;

/// <summary>
/// Abstraction for enqueueing welcome email background job (Hangfire implementation in Infrastructure).
/// </summary>
public interface IWelcomeEmailJobClient
{
    void EnqueueWelcomeEmail(Guid userId, string email, string displayName);
}
