using Hangfire;
using SaaSApp.MultiTenancy;
using SaaSApp.Workflow.Application.Contracts;

namespace SaaSApp.Workflow.Infrastructure.Jobs;

public sealed class MasterFileImportPythonJobClient : IMasterFileImportPythonJobClient
{
    private readonly ITenantDisplayResolver _tenantDisplay;

    public MasterFileImportPythonJobClient(ITenantDisplayResolver tenantDisplay)
    {
        _tenantDisplay = tenantDisplay;
    }

    public async Task<string> EnqueueAsync(MasterFileImportPythonJobArgs args, CancellationToken cancellationToken = default)
    {
        var tenantDisplay = await _tenantDisplay.ResolveAsync(args.TenantId, cancellationToken);
        var jobId = BackgroundJob.Enqueue<RunMasterFileImportPythonJob>(j =>
            j.Execute(tenantDisplay, args, null));
        return jobId;
    }
}
