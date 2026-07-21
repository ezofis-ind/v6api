using Microsoft.Extensions.Logging;
using Hangfire;
using Hangfire.Server;
using SaaSApp.MultiTenancy;

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

    [JobDisplayName("Welcome email · {0}")]
    public Task Execute(string tenantDisplay, Guid tenantId, Guid userId, string email, string displayName, PerformContext? context)
    {
        context?.SetJobParameter("TenantId", tenantId.ToString("D"));
        context?.SetJobParameter("TenantName", tenantDisplay);

        _logger.LogInformation(
            "Welcome email job: Tenant={TenantDisplay} ({TenantId}), UserId={UserId}, Email={Email}, DisplayName={DisplayName}",
            tenantDisplay,
            tenantId,
            userId,
            email,
            displayName);
        // TODO: Integrate with email provider (SendGrid, etc.)
        return Task.CompletedTask;
    }
}
