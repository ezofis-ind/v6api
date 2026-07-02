using Hangfire;
using Hangfire.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SaaSApp.MultiTenancy;
using SaaSApp.Workflow.Application.Contracts;

namespace SaaSApp.Workflow.Infrastructure.Jobs;

/// <summary>Hangfire job: POST master file import payload to Python after notification row is created.</summary>
public sealed class RunMasterFileImportPythonJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITenantConnectionStringResolver _connectionStringResolver;
    private readonly ILogger<RunMasterFileImportPythonJob> _logger;

    public RunMasterFileImportPythonJob(
        IServiceScopeFactory scopeFactory,
        ITenantConnectionStringResolver connectionStringResolver,
        ILogger<RunMasterFileImportPythonJob> logger)
    {
        _scopeFactory = scopeFactory;
        _connectionStringResolver = connectionStringResolver;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 0)]
    public async Task Execute(MasterFileImportPythonJobArgs args, PerformContext? context)
    {
        var jobId = context?.BackgroundJob.Id
            ?? throw new InvalidOperationException("Hangfire PerformContext is required for master file import jobs.");

        _logger.LogInformation(
            "Master file import job {JobId} started for process {ProcessId}, notification {NotificationId}",
            jobId,
            args.MasterFileProcessId,
            args.NotificationId);

        var connectionString = await _connectionStringResolver.GetConnectionStringAsync(args.TenantId);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Tenant connection string not found for {args.TenantId:D}.");
        }

        using var scope = _scopeFactory.CreateScope();
        var services = scope.ServiceProvider;

        services.GetRequiredService<ITenantConnectionProvider>().SetConnectionString(connectionString);
        var jobContext = services.GetRequiredService<JobExecutionContext>();
        jobContext.Set(args.TenantId, args.UserId);

        try
        {
            await services.GetRequiredService<IMasterFileImportPythonPipelineService>()
                .ExecuteAsync(args, jobId);
        }
        finally
        {
            jobContext.Clear();
        }
    }
}
