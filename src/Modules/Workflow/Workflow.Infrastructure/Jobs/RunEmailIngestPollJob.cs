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
/// Recurring Hangfire job: for each active tenant, poll due EmailIngestMailbox rows.
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

    [AutomaticRetry(Attempts = 0)]
    public async Task Execute(PerformContext? context)
    {
        await using var catalog = await _catalogFactory.CreateDbContextAsync();
        var tenants = await catalog.Tenants
            .AsNoTracking()
            .Where(t => t.IsActive)
            .Select(t => new { t.Id, t.Name })
            .ToListAsync();

        foreach (var tenant in tenants)
        {
            try
            {
                var connectionString = await _connectionStringResolver.GetConnectionStringAsync(tenant.Id);
                if (string.IsNullOrWhiteSpace(connectionString))
                    continue;

                using var scope = _scopeFactory.CreateScope();
                var services = scope.ServiceProvider;
                services.GetRequiredService<ITenantConnectionProvider>().SetConnectionString(connectionString);
                var jobContext = services.GetRequiredService<JobExecutionContext>();
                jobContext.Set(tenant.Id, SystemUserId);

                try
                {
                    var ingest = services.GetRequiredService<IEmailIngestService>();
                    var results = await ingest.PollDueMailboxesAsync();
                    var started = results.Sum(r => r.AttachmentsStarted);
                    if (started > 0 || results.Any(r => r.Error != null))
                    {
                        _logger.LogInformation(
                            "Email ingest tenant {TenantId} ({TenantName}): {MailboxCount} mailbox(es), {Started} attachment(s) started",
                            tenant.Id,
                            tenant.Name,
                            results.Count,
                            started);
                    }
                }
                finally
                {
                    jobContext.Clear();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Email ingest poll failed for tenant {TenantId}", tenant.Id);
            }
        }
    }
}
