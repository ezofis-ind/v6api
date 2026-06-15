using SaaSApp.Users.Application.Contracts;

namespace SaaSApp.Users.Infrastructure.Jobs;

public sealed class WelcomeEmailJobClient : IWelcomeEmailJobClient
{
    public void EnqueueWelcomeEmail(Guid userId, string email, string displayName)
    {
        // Hangfire will resolve the job type and enqueue it
        Hangfire.BackgroundJob.Enqueue<SendWelcomeEmailJob>(j =>
            j.Execute(userId, email, displayName));
    }
}
