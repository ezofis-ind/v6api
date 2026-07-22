using Hangfire;
using Hangfire.Server;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SaaSApp.Catalog.Persistence;
using SaaSApp.MultiTenancy;
using SaaSApp.Workflow.Application.Contracts;
using SaaSApp.Workflow.Infrastructure.Options;

namespace SaaSApp.Workflow.Infrastructure.Jobs;

/// <summary>
/// Hangfire email ingest: polls ONLY tenants that have an enabled mailbox.
/// Does not enqueue a job per active tenant on every tick.
/// </summary>
public sealed class RunEmailIngestPollJob
{
    private static readonly Guid SystemUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDbContextFactory<CatalogDbContext> _catalogFactory;
    private readonly ITenantConnectionStringResolver _connectionStringResolver;
    private readonly IOptions<EmailIngestOptions> _options;
    private readonly ILogger<RunEmailIngestPollJob> _logger;

    public RunEmailIngestPollJob(
        IServiceScopeFactory scopeFactory,
        IDbContextFactory<CatalogDbContext> catalogFactory,
        ITenantConnectionStringResolver connectionStringResolver,
        IOptions<EmailIngestOptions> options,
        ILogger<RunEmailIngestPollJob> logger)
    {
        _scopeFactory = scopeFactory;
        _catalogFactory = catalogFactory;
        _connectionStringResolver = connectionStringResolver;
        _options = options;
        _logger = logger;
    }

    /// <summary>Recurring entry: enqueue polls only for indexed mailbox tenants.</summary>
    [AutomaticRetry(Attempts = 0)]
    [JobDisplayName("Email ingest · schedule mailboxes")]
    public async Task Execute(PerformContext? context)
    {
        var opts = _options.Value;
        if (!opts.HangfireEnabled)
        {
            _logger.LogInformation("Email ingest Hangfire disabled by config — skip schedule");
            return;
        }

        EmailIngestTenantIndex.FullScanInterval = TimeSpan.FromMinutes(Math.Max(5, opts.TenantDiscoveryMinutes));

        if (EmailIngestTenantIndex.NeedsFullScan())
            await DiscoverMailboxTenantsAsync();

        var snapshot = EmailIngestTenantIndex.Snapshot();
        if (snapshot.Count == 0)
        {
            _logger.LogInformation(
                "Email ingest schedule: no tenants with enabled mailboxes — nothing to enqueue");
            return;
        }

        var enqueued = 0;
        foreach (var (tenantId, tenantName) in snapshot)
        {
            BackgroundJob.Enqueue<RunEmailIngestPollJob>(j =>
                j.ExecuteForTenant(tenantId, tenantName, null));
            enqueued++;
        }

        _logger.LogInformation(
            "Email ingest schedule: enqueued {Enqueued} mailbox tenant(s) (not all tenants)",
            enqueued);
    }

    /// <summary>Per-tenant poll — only for tenants known to have mailboxes.</summary>
    [AutomaticRetry(Attempts = 0)]
    [JobDisplayName("Email ingest · {1}")]
    public async Task ExecuteForTenant(Guid tenantId, string tenantName, PerformContext? context)
    {
        var label = string.IsNullOrWhiteSpace(tenantName) ? tenantId.ToString("D") : tenantName.Trim();

        try
        {
            context?.SetJobParameter("TenantId", tenantId.ToString("D"));
            context?.SetJobParameter("TenantName", label);

            var connectionString = await _connectionStringResolver.GetConnectionStringAsync(tenantId);
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                _logger.LogWarning(
                    "Email ingest tenant {TenantId} ({TenantName}): no connection string — unregister",
                    tenantId,
                    label);
                EmailIngestTenantIndex.Unregister(tenantId);
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var services = scope.ServiceProvider;
            services.GetRequiredService<ITenantConnectionProvider>().SetConnectionString(connectionString);
            var jobContext = services.GetRequiredService<JobExecutionContext>();
            jobContext.Set(tenantId, SystemUserId);

            try
            {
                var ingest = services.GetRequiredService<IEmailIngestService>();
                if (!await ingest.HasEnabledMailboxesAsync())
                {
                    _logger.LogInformation(
                        "Email ingest tenant {TenantId} ({TenantName}): no enabled mailboxes — unregister",
                        tenantId,
                        label);
                    EmailIngestTenantIndex.Unregister(tenantId);
                    return;
                }

                EmailIngestTenantIndex.Register(tenantId, label);

                var mailboxes = await ingest.ListMailboxesAsync();
                var enabled = mailboxes.Where(m => m.IsEnabled).ToList();

                foreach (var mb in enabled.Where(m => !string.IsNullOrWhiteSpace(m.LastError)))
                {
                    _logger.LogWarning(
                        "Email ingest tenant {TenantId} ({TenantName}) mailbox {MailboxId} lastError: {Error}",
                        tenantId,
                        label,
                        mb.Id,
                        mb.LastError);
                }

                var results = await ingest.PollDueMailboxesAsync();
                var started = results.Sum(r => r.AttachmentsStarted);
                var skipped = results.Sum(r => r.SkippedAlreadyProcessed);
                var scanned = results.Sum(r => r.MessagesScanned);
                var errors = results.Where(r => !string.IsNullOrWhiteSpace(r.Error)).Select(r => r.Error!).ToList();

                _logger.LogInformation(
                    "Email ingest tenant {TenantId} ({TenantName}): enabled={Enabled}, due={Due}, scanned={Scanned}, started={Started}, skipped={Skipped}, errors={ErrorCount}",
                    tenantId,
                    label,
                    enabled.Count,
                    results.Count,
                    scanned,
                    started,
                    skipped,
                    errors.Count);

                foreach (var err in errors)
                {
                    _logger.LogWarning(
                        "Email ingest tenant {TenantId} ({TenantName}) mailbox error: {Error}",
                        tenantId,
                        label,
                        err);
                }
            }
            finally
            {
                jobContext.Clear();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Email ingest poll failed for tenant {TenantId} ({TenantName})", tenantId, label);
            throw;
        }
    }

    private async Task DiscoverMailboxTenantsAsync()
    {
        await using var catalog = await _catalogFactory.CreateDbContextAsync();
        var tenants = await catalog.Tenants
            .AsNoTracking()
            .Where(t => t.IsActive)
            .Select(t => new { t.Id, t.Name })
            .ToListAsync();

        var found = new List<(Guid, string)>();
        foreach (var tenant in tenants)
        {
            var connectionString = await _connectionStringResolver.GetConnectionStringAsync(tenant.Id);
            if (string.IsNullOrWhiteSpace(connectionString))
                continue;

            if (!await TenantHasEnabledMailboxAsync(tenant.Id, connectionString))
                continue;

            found.Add((tenant.Id, tenant.Name ?? tenant.Id.ToString("D")));
        }

        EmailIngestTenantIndex.ReplaceAll(found);
        _logger.LogInformation(
            "Email ingest discovery: {Found} tenant(s) with enabled mailboxes out of {Active} active (next full scan in {Minutes}m)",
            found.Count,
            tenants.Count,
            Math.Max(5, _options.Value.TenantDiscoveryMinutes));
    }

    private async Task<bool> TenantHasEnabledMailboxAsync(Guid tenantId, string connectionString)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var services = scope.ServiceProvider;
            services.GetRequiredService<ITenantConnectionProvider>().SetConnectionString(connectionString);
            var jobContext = services.GetRequiredService<JobExecutionContext>();
            jobContext.Set(tenantId, SystemUserId);
            try
            {
                return await services.GetRequiredService<IEmailIngestService>().HasEnabledMailboxesAsync();
            }
            finally
            {
                jobContext.Clear();
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Email ingest discovery skip tenant {TenantId}", tenantId);
            return false;
        }
    }
}
