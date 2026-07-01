namespace SaaSApp.Workflow.Application.Contracts;

public sealed record MasterFileImportPythonJobArgs(
    Guid TenantId,
    Guid UserId,
    int MasterFileProcessId,
    int NotificationId,
    string PayloadJson);

public interface IMasterFileImportPythonJobClient
{
    Task<string> EnqueueAsync(MasterFileImportPythonJobArgs args, CancellationToken cancellationToken = default);
}

public interface IMasterFileImportPythonPipelineService
{
    Task ExecuteAsync(MasterFileImportPythonJobArgs args, string? hangfireJobId = null, CancellationToken cancellationToken = default);
}
