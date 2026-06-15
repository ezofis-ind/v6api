using System.Text.Json;

namespace SaaSApp.Repository.Application;

/// <summary>
/// Builds metadata for multipart repository uploads from the <c>metadata</c> JSON field
/// plus any extra form fields (e.g. Supplier, PoNumber) clients often send separately.
/// </summary>
public static class RepositoryFormMetadataCollector
{
    private static readonly HashSet<string> ReservedFormKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "file",
        "workflowId",
        "processId",
        "instanceId",
        "transactionId",
        "storageProviderCode",
        "metadata",
        "contentType",
        "ContentDisposition",
        "ContentType"
    };

    public static IReadOnlyDictionary<string, string> Collect(
        string? metadataJson,
        IEnumerable<KeyValuePair<string, string?>>? additionalFormFields = null)
    {
        var merged = new Dictionary<string, string>(
            RepositoryMetadataParser.Parse(metadataJson),
            StringComparer.OrdinalIgnoreCase);

        if (additionalFormFields == null)
            return merged;

        foreach (var (key, value) in additionalFormFields)
        {
            if (string.IsNullOrWhiteSpace(key) || ReservedFormKeys.Contains(key))
                continue;

            var fieldName = UnwrapMetadataFieldName(key);
            if (string.IsNullOrWhiteSpace(fieldName) || ReservedFormKeys.Contains(fieldName))
                continue;

            if (string.IsNullOrWhiteSpace(value))
                continue;

            var trimmed = value.Trim();
            if (trimmed.StartsWith('{') && trimmed.EndsWith('}'))
            {
                foreach (var nested in RepositoryMetadataParser.Parse(trimmed))
                    merged[nested.Key] = nested.Value;
                continue;
            }

            merged[fieldName] = trimmed;
        }

        return merged;
    }

    public static string? ToMetadataJson(IReadOnlyDictionary<string, string> metadata) =>
        metadata.Count == 0
            ? null
            : JsonSerializer.Serialize(metadata);

    private static string UnwrapMetadataFieldName(string key)
    {
        var trimmed = key.Trim();
        if (trimmed.StartsWith("metadata[", StringComparison.OrdinalIgnoreCase) && trimmed.EndsWith(']'))
            return trimmed[9..^1].Trim();

        return trimmed;
    }
}
