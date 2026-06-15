namespace SaaSApp.Workflow.Application.Contracts;

public sealed record ApAgentJobProgressUpdate(
    string? Stage = null,
    string? Message = null,
    int? Percent = null);

public sealed record ApAgentJobStatusResult(
    string JobId,
    Guid WorkflowId,
    Guid InstanceId,
    string HangfireStatus,
    string? Stage,
    string? Message,
    int? Percent,
    string? ErrorMessage,
    DateTime? UpdatedAtUtc,
    bool IsTerminal);

public interface IApAgentJobProgressService
{
    Task RegisterQueuedAsync(
        string jobId,
        Guid tenantId,
        Guid workflowId,
        Guid instanceId,
        CancellationToken cancellationToken = default);

    Task UpdateProgressAsync(
        string jobId,
        ApAgentJobProgressUpdate update,
        CancellationToken cancellationToken = default);

    Task UpdateProgressByInstanceAsync(
        Guid workflowId,
        Guid instanceId,
        ApAgentJobProgressUpdate update,
        CancellationToken cancellationToken = default);

    Task SetHangfireStateAsync(
        string jobId,
        string hangfireState,
        string? message = null,
        string? errorMessage = null,
        CancellationToken cancellationToken = default);

    Task<ApAgentJobProgressRow?> GetByJobIdAsync(string jobId, CancellationToken cancellationToken = default);

    Task<string?> GetLatestActiveJobIdForInstanceAsync(
        Guid instanceId,
        CancellationToken cancellationToken = default);
}

public sealed record ApAgentJobProgressRow(
    string JobId,
    Guid TenantId,
    Guid WorkflowId,
    Guid InstanceId,
    string? HangfireState,
    string? Stage,
    string? Message,
    int? Percent,
    string? ErrorMessage,
    DateTime UpdatedAtUtc);

public interface IApAgentJobStatusService
{
    Task<ApAgentJobStatusResult?> GetStatusAsync(string jobId, CancellationToken cancellationToken = default);
}
