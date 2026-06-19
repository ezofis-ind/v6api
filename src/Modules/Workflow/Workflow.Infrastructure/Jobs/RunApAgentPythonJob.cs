using Hangfire;
using Hangfire.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SaaSApp.MultiTenancy;
using SaaSApp.Workflow.Application.Contracts;

namespace SaaSApp.Workflow.Infrastructure.Jobs;

/// <summary>Hangfire job: POST start payload to Python AP Agent (move-next is called by Python).</summary>
public sealed class RunApAgentPythonJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITenantConnectionStringResolver _connectionStringResolver;
    private readonly ILogger<RunApAgentPythonJob> _logger;

    public RunApAgentPythonJob(
        IServiceScopeFactory scopeFactory,
        ITenantConnectionStringResolver connectionStringResolver,
        ILogger<RunApAgentPythonJob> logger)
    {
        _scopeFactory = scopeFactory;
        _connectionStringResolver = connectionStringResolver;
        _logger = logger;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    [AutomaticRetry(Attempts = 0)]
    public async Task Execute(ApAgentPythonJobArgs args, PerformContext? context)
    {
        var jobId = context?.BackgroundJob.Id
            ?? throw new InvalidOperationException("Hangfire PerformContext is required for AP Agent jobs.");
        _logger.LogInformation(
            "AP Agent Python job {JobId} started for workflow {WorkflowId}, instance {InstanceId}",
            jobId,
            args.WorkflowId,
            args.InstanceId);

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

        var progress = services.GetRequiredService<IApAgentJobProgressService>();

        try
        {
            await progress.SetHangfireStateAsync(
                jobId,
                "Processing",
                "Calling AP Agent service",
                cancellationToken: default);
            await progress.UpdateProgressAsync(
                jobId,
                new ApAgentJobProgressUpdate("PROCESSING", "AP Agent processing started"),
                default);

            await services.GetRequiredService<IApAgentPythonPipelineService>()
                .ExecuteAsync(args, jobId);

            _logger.LogInformation(
                "AP Agent Python job {JobId} POST finished for instance {InstanceId}; awaiting COMPLETED/FAILED from Python.",
                jobId,
                args.InstanceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "AP Agent Python job {JobId} failed for instance {InstanceId}",
                jobId,
                args.InstanceId);

            try
            {
                await progress.SetHangfireStateAsync(
                    jobId,
                    "Failed",
                    "AP Agent failed",
                    ex.Message);
                await progress.UpdateProgressAsync(
                    jobId,
                    new ApAgentJobProgressUpdate("FAILED", ex.Message),
                    default);
            }
            catch (Exception progressEx)
            {
                _logger.LogWarning(progressEx, "Failed to persist AP Agent job failure for {JobId}", jobId);
            }

            throw;
        }
        finally
        {
            jobContext.Clear();
        }
    }
}
