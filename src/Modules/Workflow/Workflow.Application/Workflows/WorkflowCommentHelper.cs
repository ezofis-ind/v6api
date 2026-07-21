using System.Text.Json;
using System.Text.RegularExpressions;

namespace SaaSApp.Workflow.Application.Workflows;

/// <summary>
/// Hides automatic v5 rule/proceed echoes from the comment thread
/// (e.g. <c>2MH_BMDFEVKsU0uAQjol1: Matched</c>), while allowing normal user text
/// such as <c>verified</c> / <c>test</c>.
/// </summary>
public static class WorkflowCommentHelper
{
    private static readonly Regex RuleProceedCommentRegex = new(
        @"^[\w-]{8,}:\s*.+$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>
    /// True for auto-posted rule review echoes (<c>ruleId: Matched</c> / JSON), not free-text comments.
    /// </summary>
    public static bool IsAutomaticRuleProceedComment(string? comments)
    {
        if (string.IsNullOrWhiteSpace(comments))
            return false;

        var trimmed = comments.Trim();

        if (TryParseRuleProceedJson(trimmed))
            return true;

        return RuleProceedCommentRegex.IsMatch(trimmed);
    }

    private static bool TryParseRuleProceedJson(string trimmed)
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

            // e.g. { "2MH_BMDFEVKsU0uAQjol1": "Matched" }
            return properties.All(p =>
                p.Name.Length >= 8
                && p.Value.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(p.Value.GetString()));
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
