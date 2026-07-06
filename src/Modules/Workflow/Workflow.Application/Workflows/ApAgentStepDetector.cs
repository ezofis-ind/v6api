using SaaSApp.Workflow.Domain.Entities;

namespace SaaSApp.Workflow.Application.Workflows;

/// <summary>Detects AP Agent move-next steps (including legacy stage/activity ids).</summary>
public static class ApAgentStepDetector
{
    public const string LegacyApAgentActivityId = "DR97uPaylMtwahvi3XYr_";

    public static bool IsApAgentMoveNext(WorkflowStep step, string requestActivityId)
    {
        if (WorkflowStepTransitionHelper.IsApAgentStep(step))
            return true;

        if (MatchesApAgentOneLabel(step.Name) || MatchesApAgentOneLabel(step.StageType))
            return true;

        if (!string.IsNullOrWhiteSpace(step.ActivityId)
            && string.Equals(step.ActivityId.Trim(), LegacyApAgentActivityId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var activityId = requestActivityId?.Trim() ?? string.Empty;
        return string.Equals(activityId, LegacyApAgentActivityId, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesApAgentOneLabel(string? value) =>
        string.Equals(value?.Trim(), "ap agent 1", StringComparison.OrdinalIgnoreCase);
}
