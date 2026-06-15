using System.Text.Json;

namespace SaaSApp.Repository.Application;

/// <summary>Parse repository item metadata JSON (upload form field or PATCH body).</summary>
public static class RepositoryMetadataParser
{
    public static IReadOnlyDictionary<string, string> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var doc = JsonDocument.Parse(json.Trim());
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException(
                    "metadata must be a JSON object, e.g. {\"Supplier\":\"Acme\",\"InvoiceNumber\":\"INV-1\"}.");
            }

            var dict = ParseObject(doc.RootElement);
            if (dict.Count == 1 && dict.TryGetValue("metadata", out var nested) && nested.TrimStart().StartsWith('{'))
                return Parse(nested);
            if (dict.Count == 1 && dict.TryGetValue("fields", out var fields) && fields.TrimStart().StartsWith('{'))
                return Parse(fields);

            return dict;
        }
        catch (JsonException ex)
        {
            throw new ArgumentException(
                $"metadata must be a JSON object, e.g. {{\"Supplier\":\"Acme Supplies\"}}. {ex.Message}");
        }
    }

    private static Dictionary<string, string> ParseObject(JsonElement root)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in root.EnumerateObject())
        {
            if (string.IsNullOrWhiteSpace(prop.Name))
                continue;

            if (prop.Value.ValueKind == JsonValueKind.Object)
            {
                foreach (var nested in ParseObject(prop.Value))
                    dict[nested.Key] = nested.Value;
                continue;
            }

            var value = ToString(prop.Value);
            if (string.IsNullOrWhiteSpace(value))
                continue;

            dict[prop.Name.Trim()] = value;
        }

        return dict;
    }

    private static string? ToString(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.GetRawText(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Null => null,
        _ => element.ToString()
    };
}
