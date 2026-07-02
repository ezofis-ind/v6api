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

        return await BuildStatusAsync(jobId.Trim(), cancellationToken);
    }

    public async Task<ApAgentJobStatusListResult> GetStatusesAsync(
        IEnumerable<string> jobIds,
        CancellationToken cancellationToken = default)
    {
        var uniqueIds = jobIds
            .Select(id => id?.Trim())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Cast<string>()
            .ToList();

        if (uniqueIds.Count == 0)
            return new ApAgentJobStatusListResult(Array.Empty<ApAgentJobStatusResult>(), Array.Empty<string>());

        var items = new List<ApAgentJobStatusResult>(uniqueIds.Count);
        var notFound = new List<string>();

        foreach (var id in uniqueIds)
        {
            var status = await BuildStatusAsync(id, cancellationToken);
            if (status == null)
                notFound.Add(id);
            else
                items.Add(status);
        }

        return new ApAgentJobStatusListResult(items, notFound);
    }

    private async Task<ApAgentJobStatusResult?> BuildStatusAsync(string jobId, CancellationToken cancellationToken)
    {
        var row = await _progress.GetByJobIdAsync(jobId, cancellationToken);
        // DB state drives UI: Hangfire marks Succeeded when POST returns; Python PATCH sets terminal state.
        var hangfireState = row?.HangfireState ?? ResolveHangfireState(jobId) ?? "Unknown";

        if (row == null && hangfireState == "Unknown")
            return null;

        var isTerminal = TerminalStates.Contains(hangfireState)
            || string.Equals(row?.Stage, "COMPLETED", StringComparison.OrdinalIgnoreCase)
            || string.Equals(row?.Stage, "FAILED", StringComparison.OrdinalIgnoreCase);
        var errorMessage = row?.ErrorMessage;
        if (string.Equals(hangfireState, "Failed", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(errorMessage))
        {
            errorMessage = TryGetHangfireExceptionMessage(jobId);
        }

        return new ApAgentJobStatusResult(
            jobId,
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
