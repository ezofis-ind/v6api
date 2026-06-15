namespace SaaSApp.Workflow.Application.Contracts;

/// <summary>Arguments for the AP Agent Python background job (multipart start with file only).</summary>
public sealed record ApAgentPythonJobArgs(
    Guid TenantId,
    Guid UserId,
    Guid WorkflowId,
    Guid InstanceId,
    string StartPayloadJson);

public interface IApAgentPythonJobClient
{
    Task<string> EnqueueAsync(ApAgentPythonJobArgs args, CancellationToken cancellationToken = default);
}

public interface IApAgentPythonPipelineService
{
    Task ExecuteAsync(
        ApAgentPythonJobArgs args,
        string? hangfireJobId = null,
        CancellationToken cancellationToken = default);
}
