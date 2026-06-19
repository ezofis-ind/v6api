using Hangfire;
using Hangfire.Storage;
using SaaSApp.Workflow.Application.Contracts;

namespace SaaSApp.Workflow.Infrastructure.Services;

public sealed class ApAgentJobStatusService : IApAgentJobStatusService
{
    private static readonly HashSet<string> TerminalStates = new(StringComparer.OrdinalIgnoreCase)
    {
        "Succeeded", "Failed", "Deleted"
    };

    private readonly IApAgentJobProgressService _progress;

    public ApAgentJobStatusService(IApAgentJobProgressService progress)
    {
        _progress = progress;
    }

    public async Task<ApAgentJobStatusResult?> GetStatusAsync(string jobId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobId))
            return null;

        var row = await _progress.GetByJobIdAsync(jobId.Trim(), cancellationToken);
        // DB state drives UI: Hangfire marks Succeeded when POST returns; Python PATCH sets terminal state.
        var hangfireState = row?.HangfireState ?? ResolveHangfireState(jobId.Trim()) ?? "Unknown";

        if (row == null && hangfireState == "Unknown")
            return null;

        var isTerminal = TerminalStates.Contains(hangfireState)
            || string.Equals(row?.Stage, "COMPLETED", StringComparison.OrdinalIgnoreCase)
            || string.Equals(row?.Stage, "FAILED", StringComparison.OrdinalIgnoreCase);
        var errorMessage = row?.ErrorMessage;
        if (string.Equals(hangfireState, "Failed", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(errorMessage))
        {
            errorMessage = TryGetHangfireExceptionMessage(jobId.Trim());
        }

        return new ApAgentJobStatusResult(
            jobId.Trim(),
            row?.WorkflowId ?? Guid.Empty,
            row?.InstanceId ?? Guid.Empty,
            hangfireState,
            row?.Stage,
            row?.Message,
            row?.Percent,
            row?.FormData,
            errorMessage,
            row?.UpdatedAtUtc,
            isTerminal);
    }

    private static string? ResolveHangfireState(string jobId)
    {
        try
        {
            var connection = JobStorage.Current.GetConnection();
            var state = connection.GetStateData(jobId);
            return state?.Name;
        }
        catch
        {
            return null;
        }
    }

    private static string? TryGetHangfireExceptionMessage(string jobId)
    {
        try
        {
            var connection = JobStorage.Current.GetConnection();
            var state = connection.GetStateData(jobId);
            if (state?.Data != null
                && state.Data.TryGetValue("ExceptionMessage", out var message)
                && !string.IsNullOrWhiteSpace(message))
            {
                return message!;
            }
        }
        catch
        {
            // Hangfire storage may be unavailable in some hosts.
        }

        return null;
    }
}
