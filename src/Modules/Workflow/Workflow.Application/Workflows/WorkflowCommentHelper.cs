using System.Text.Json;
using System.Text.RegularExpressions;

namespace SaaSApp.Workflow.Application.Workflows;

/// <summary>Filters v5-style proceed-action echoes from user comment threads.</summary>
public static class WorkflowCommentHelper
{
    private static readonly Regex RuleProceedCommentRegex = new(
        @"^[\w-]{8,}:\s*(.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>
    /// v5 UI posts proceed/review labels to comments as plain text, <c>ruleId: Matched</c>,
    /// or JSON <c>{"ruleId":"Matched"}</c>. Those belong on move-next <c>review</c>, not comments.
    /// </summary>
    public static bool IsProceedActionSystemComment(string? comments)
    {
        if (string.IsNullOrWhiteSpace(comments))
            return false;

        var trimmed = comments.Trim();

        if (TryParseProceedActionJson(trimmed))
            return true;

        var match = RuleProceedCommentRegex.Match(trimmed);
        if (match.Success)
            return WorkflowStepActionsHelper.IsProceedActionLabel(match.Groups[1].Value);

        return WorkflowStepActionsHelper.IsProceedActionLabel(trimmed);
    }

    private static bool TryParseProceedActionJson(string trimmed)
    {
        if (!trimmed.StartsWith('{'))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return false;

            var properties = doc.RootElement.EnumerateObject().ToList();
            if (properties.Count == 0)
                return false;

            return properties.All(p =>
                p.Value.ValueKind == JsonValueKind.String
                && WorkflowStepActionsHelper.IsProceedActionLabel(p.Value.GetString()));
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
