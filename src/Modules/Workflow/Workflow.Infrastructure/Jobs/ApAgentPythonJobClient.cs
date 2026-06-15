using Hangfire;
using SaaSApp.Workflow.Application.Contracts;

namespace SaaSApp.Workflow.Infrastructure.Jobs;

public sealed class ApAgentPythonJobClient : IApAgentPythonJobClient
{
    private readonly IApAgentJobProgressService _progress;

    public ApAgentPythonJobClient(IApAgentJobProgressService progress)
    {
        _progress = progress;
    }

    public async Task<string> EnqueueAsync(ApAgentPythonJobArgs args, CancellationToken cancellationToken = default)
    {
        var jobId = BackgroundJob.Enqueue<RunApAgentPythonJob>(j => j.Execute(args, null));
        await _progress.RegisterQueuedAsync(
            jobId,
            args.TenantId,
            args.WorkflowId,
            args.InstanceId,
            cancellationToken);
        return jobId;
    }
}
