using Hangfire;
using SaaSApp.Workflow.Application.Contracts;

namespace SaaSApp.Workflow.Infrastructure.Jobs;

public sealed class MasterFileImportPythonJobClient : IMasterFileImportPythonJobClient
{
    public Task<string> EnqueueAsync(MasterFileImportPythonJobArgs args, CancellationToken cancellationToken = default)
    {
        var jobId = BackgroundJob.Enqueue<RunMasterFileImportPythonJob>(j => j.Execute(args, null));
        return Task.FromResult(jobId);
    }
}
