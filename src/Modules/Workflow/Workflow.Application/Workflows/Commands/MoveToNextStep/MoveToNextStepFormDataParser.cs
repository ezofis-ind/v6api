using System.Text.Json;

namespace SaaSApp.Workflow.Application.Workflows.Commands.MoveToNextStep;

/// <summary>Parses move-next <c>formData</c> (jsonId → value map) for ezfb updates.</summary>
public static class MoveToNextStepFormDataParser
{
    private static readonly HashSet<string> ReservedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "formId", "formEntryId", "formentryId", "workflowId", "instanceId", "transactionId"
    };

    public static IReadOnlyDictionary<string, string>? ParseFieldValues(JsonElement? formData)
    {
        if (!formData.HasValue)
            return null;

        var root = formData.Value;
        if (root.ValueKind == JsonValueKind.String)
        {
            var text = root.GetString();
            if (string.IsNullOrWhiteSpace(text))
                return null;

            using var doc = JsonDocument.Parse(text);
            return ParseObject(doc.RootElement);
        }

        if (root.ValueKind == JsonValueKind.Object)
            return ParseObject(root);

        return null;
    }

    private static Dictionary<string, string>? ParseObject(JsonElement obj)
    {
        if (obj.ValueKind != JsonValueKind.Object)
            return null;

        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in obj.EnumerateObject())
        {
            if (ReservedKeys.Contains(prop.Name))
                continue;

            if (!TryGetScalarString(prop.Value, out var value))
                continue;

            if (string.IsNullOrWhiteSpace(value))
                continue;

            dict[prop.Name] = value;
        }

        return dict.Count > 0 ? dict : null;
    }

    private static bool TryGetScalarString(JsonElement value, out string? result)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                result = null;
                return false;
            case JsonValueKind.String:
                result = value.GetString();
                return true;
            case JsonValueKind.Number:
            case JsonValueKind.True:
            case JsonValueKind.False:
                result = value.GetRawText();
                return true;
            default:
                result = value.GetRawText();
                return true;
        }
    }
}
