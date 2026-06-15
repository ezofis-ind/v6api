using Microsoft.Extensions.Logging;

namespace SaaSApp.Users.Infrastructure.Jobs;

/// <summary>
/// Background job: send welcome email after user creation (Hangfire).
/// </summary>
public sealed class SendWelcomeEmailJob
{
    private readonly ILogger<SendWelcomeEmailJob> _logger;

    public SendWelcomeEmailJob(ILogger<SendWelcomeEmailJob> logger)
    {
        _logger = logger;
    }

    public Task Execute(Guid userId, string email, string displayName)
    {
        _logger.LogInformation(
            "Welcome email job: UserId={UserId}, Email={Email}, DisplayName={DisplayName}",
            userId, email, displayName);
        // TODO: Integrate with email provider (SendGrid, etc.)
        return Task.CompletedTask;
    }
}
