using Hangfire;
using SaaSApp.MultiTenancy;
using SaaSApp.Users.Application.Contracts;

namespace SaaSApp.Users.Infrastructure.Jobs;

public sealed class WelcomeEmailJobClient : IWelcomeEmailJobClient
{
    private readonly ITenantDisplayResolver _tenantDisplay;

    public WelcomeEmailJobClient(ITenantDisplayResolver tenantDisplay)
    {
        _tenantDisplay = tenantDisplay;
    }

    public async Task EnqueueWelcomeEmailAsync(
        Guid tenantId,
        Guid userId,
        string email,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        var tenantDisplay = await _tenantDisplay.ResolveAsync(tenantId, cancellationToken);
        BackgroundJob.Enqueue<SendWelcomeEmailJob>(j =>
            j.Execute(tenantDisplay, tenantId, userId, email, displayName, null));
    }
}
