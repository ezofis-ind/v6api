namespace SaaSApp.Workflow.Infrastructure.Options;

public sealed class EmailIngestOptions
{
    public const string SectionName = "EmailIngest";

    /// <summary>When false, Hangfire email-ingest recurring job is not registered.</summary>
    public bool HangfireEnabled { get; set; } = true;

    /// <summary>
    /// Hangfire cron for the scheduler. Default every 5 minutes (not every minute).
    /// Only tenants with an enabled mailbox are polled.
    /// </summary>
    public string HangfireCron { get; set; } = "*/5 * * * *";

    /// <summary>How often Hangfire may scan all tenants to discover new mailboxes (minutes).</summary>
    public int TenantDiscoveryMinutes { get; set; } = 30;
}
