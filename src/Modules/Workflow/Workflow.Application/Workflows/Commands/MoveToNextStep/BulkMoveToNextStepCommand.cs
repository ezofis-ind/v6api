using MediatR;

namespace SaaSApp.Workflow.Application.Workflows.Commands.MoveToNextStep;

/// <summary>Bulk move-next: comma-separated instance ids, activityId + review; no formData.</summary>
public sealed record BulkMoveToNextStepCommand(
    IReadOnlyList<Guid> InstanceIds,
    string ActivityId,
    string? Review = null,
    string? Comments = null,
    Guid? ActivityUserId = null) : IRequest<BulkMoveToNextStepCommandResult>;

public sealed record BulkMoveToNextStepCommandResult(
    int Total,
    int Succeeded,
    int Failed,
    IReadOnlyList<BulkMoveToNextStepItemResult> Results);

public sealed record BulkMoveToNextStepItemResult(
    Guid InstanceId,
    bool Success,
    string Message,
    bool WorkflowCompleted,
    string? Error = null);

public static class BulkMoveInstanceIdParser
{
    public static IReadOnlyList<Guid> Parse(string? commaSeparated, IReadOnlyList<Guid>? instanceIds)
    {
        var ids = new List<Guid>();

        if (instanceIds is { Count: > 0 })
        {
            foreach (var id in instanceIds)
            {
                if (id != Guid.Empty && !ids.Contains(id))
                    ids.Add(id);
            }
        }

        if (!string.IsNullOrWhiteSpace(commaSeparated))
        {
            foreach (var part in commaSeparated.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!Guid.TryParse(part, out var guid) || guid == Guid.Empty)
                    throw new ArgumentException($"Invalid instance id '{part}'.");

                if (!ids.Contains(guid))
                    ids.Add(guid);
            }
        }

        if (ids.Count == 0)
            throw new ArgumentException("At least one instance id is required (instanceId comma list or instanceIds array).");

        return ids;
    }
}
