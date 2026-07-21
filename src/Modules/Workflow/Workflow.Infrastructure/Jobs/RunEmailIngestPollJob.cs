using Hangfire;
using Hangfire.Server;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SaaSApp.Catalog.Persistence;
using SaaSApp.MultiTenancy;
using SaaSApp.Workflow.Application.Contracts;

namespace SaaSApp.Workflow.Infrastructure.Jobs;

/// <summary>
/// Recurring Hangfire job: schedules one per-tenant email-ingest poll so the dashboard
/// shows tenant name/id (jobs run for every active tenant).
/// </summary>
public sealed class RunEmailIngestPollJob
{
    private static readonly Guid SystemUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDbContextFactory<CatalogDbContext> _catalogFactory;
    private readonly ITenantConnectionStringResolver _connectionStringResolver;
    private readonly ILogger<RunEmailIngestPollJob> _logger;

    public RunEmailIngestPollJob(
        IServiceScopeFactory scopeFactory,
        IDbContextFactory<CatalogDbContext> catalogFactory,
        ITenantConnectionStringResolver connectionStringResolver,
        ILogger<RunEmailIngestPollJob> logger)
    {
        _scopeFactory = scopeFactory;
        _catalogFactory = catalogFactory;
        _connectionStringResolver = connectionStringResolver;
        _logger = logger;
    }

    /// <summary>Recurring entry: enqueue one visible Hangfire job per active tenant.</summary>
    [AutomaticRetry(Attempts = 0)]
    [JobDisplayName("Email ingest · schedule all tenants")]
    public async Task Execute(PerformContext? context)
    {
        await using var catalog = await _catalogFactory.CreateDbContextAsync();
        var tenants = await catalog.Tenants
            .AsNoTracking()
            .Where(t => t.IsActive)
            .Select(t => new { t.Id, t.Name })
            .OrderBy(t => t.Name)
            .ToListAsync();

        _logger.LogInformation(
            "Email ingest schedule: enqueueing polls for {TenantCount} active tenant(s)",
            tenants.Count);

        var enqueued = 0;
        foreach (var tenant in tenants)
        {
            var connectionString = await _connectionStringResolver.GetConnectionStringAsync(tenant.Id);
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                _logger.LogDebug(
                    "Email ingest skip tenant {TenantId} ({TenantName}): no connection string",
                    tenant.Id,
                    tenant.Name);
                continue;
            }

            // Separate Hangfire job so dashboard lists tenant name (arg {1}) and id (arg {0}).
            BackgroundJob.Enqueue<RunEmailIngestPollJob>(j =>
                j.ExecuteForTenant(tenant.Id, tenant.Name ?? tenant.Id.ToString("D"), null));
            enqueued++;
        }

        _logger.LogInformation("Email ingest schedule: enqueued {Enqueued} tenant poll job(s)", enqueued);
    }

    /// <summary>Per-tenant poll — appears in Hangfire as "Email ingest · {TenantName}".</summary>
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
                    "Email ingest tenant {TenantId} ({TenantName}): no connection string",
                    tenantId,
                    label);
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
                var results = await ingest.PollDueMailboxesAsync();
                var started = results.Sum(r => r.AttachmentsStarted);
                var skipped = results.Sum(r => r.SkippedAlreadyProcessed);
                var scanned = results.Sum(r => r.MessagesScanned);
                var errors = results.Where(r => !string.IsNullOrWhiteSpace(r.Error)).Select(r => r.Error!).ToList();

                if (started > 0 || errors.Count > 0 || results.Count > 0)
                {
                    _logger.LogInformation(
                        "Email ingest tenant {TenantId} ({TenantName}): mailboxes={MailboxCount}, scanned={Scanned}, started={Started}, skipped={Skipped}, errors={ErrorCount}",
                        tenantId,
                        label,
                        results.Count,
                        scanned,
                        started,
                        skipped,
                        errors.Count);
                }

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
}
