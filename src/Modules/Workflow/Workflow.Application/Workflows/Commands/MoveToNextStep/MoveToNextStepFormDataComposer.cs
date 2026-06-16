using System.Text.Json;

namespace SaaSApp.Workflow.Application.Workflows.Commands.MoveToNextStep;

/// <summary>Uses move-next <c>formData</c> from the user for inbox/sent/completed (not AIAGENTResponse).</summary>
public static class MoveToNextStepFormDataComposer
{
    /// <summary>Returns user-submitted formData JSON as stored for mailbox tables.</summary>
    public static string? ForMailbox(string? submittedFormDataJson)
    {
        if (string.IsNullOrWhiteSpace(submittedFormDataJson))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(submittedFormDataJson);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                ? doc.RootElement.GetRawText()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Build mailbox JSON from parsed formData fields when body sent formData as object.</summary>
    public static string? FromParsedFields(
        IReadOnlyDictionary<string, string>? parsedFields,
        string? lineItemsJson)
    {
        if (parsedFields == null || parsedFields.Count == 0)
            return string.IsNullOrWhiteSpace(lineItemsJson) ? null : BuildFromLineItemsOnly(lineItemsJson);

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in parsedFields)
        {
            if (string.IsNullOrWhiteSpace(value))
                continue;
            map[key] = value;
        }

        if (!string.IsNullOrWhiteSpace(lineItemsJson))
        {
            var lineItemKey = map.Keys.FirstOrDefault(k => IsJsonArrayValue(map[k]))
                ?? map.Keys.FirstOrDefault(k => k.Contains('_', StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(lineItemKey))
                map[lineItemKey] = lineItemsJson;
        }

        return map.Count == 0 ? null : JsonSerializer.Serialize(map);
    }

    private static string? BuildFromLineItemsOnly(string lineItemsJson) =>
        string.IsNullOrWhiteSpace(lineItemsJson) ? null : $"{{\"Line Item\":{lineItemsJson}}}";

    private static bool IsJsonArrayValue(string value)
    {
        var trimmed = value.Trim();
        return trimmed.StartsWith("[", StringComparison.Ordinal)
            && trimmed.EndsWith("]", StringComparison.Ordinal);
    }
}
