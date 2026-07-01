using SaaSApp.Repository.Application.Contracts;
using SaaSApp.Repository.Infrastructure.Storage;

namespace SaaSApp.Repository.Infrastructure.Services;

internal static class RepositoryFolderMetadataResolver
{
    public const string DefaultMissingSegment = "Inbox";

    public static string DefaultSegmentForMissingField(RepositoryFieldDto field) =>
        DefaultMissingSegment;

    public static string? ResolveSegmentName(
        IReadOnlyDictionary<string, string> metadata,
        RepositoryFieldDto field)
    {
        var candidates = BuildKeyCandidates(field);
        foreach (var key in candidates)
        {
            if (TryGetMetadataValue(metadata, key, out var value))
                return RepositoryFilePathHelper.SanitizePathSegment(value);
        }

        var fieldKeys = new HashSet<string>(candidates, StringComparer.OrdinalIgnoreCase);
        foreach (var (metaKey, metaValue) in metadata)
        {
            if (string.IsNullOrWhiteSpace(metaValue))
                continue;

            if (fieldKeys.Contains(metaKey))
                return RepositoryFilePathHelper.SanitizePathSegment(metaValue);

            if (KeysMatch(field.SqlColumnName, metaKey) || KeysMatch(field.Name, metaKey))
                return RepositoryFilePathHelper.SanitizePathSegment(metaValue);
        }

        return null;
    }

    private static IReadOnlyDictionary<string, string> ToReadOnly(IDictionary<string, string> metadata) =>
        metadata as IReadOnlyDictionary<string, string>
        ?? new Dictionary<string, string>(metadata, StringComparer.OrdinalIgnoreCase);

    private static IEnumerable<string> BuildKeyCandidates(RepositoryFieldDto field)
    {
        if (!string.IsNullOrWhiteSpace(field.SqlColumnName))
            yield return field.SqlColumnName.Trim();

        if (!string.IsNullOrWhiteSpace(field.Name))
            yield return field.Name.Trim();

        foreach (var source in new[] { field.SqlColumnName, field.Name })
        {
            if (string.IsNullOrWhiteSpace(source))
                continue;

            yield return source.Replace(" ", "", StringComparison.Ordinal);
            yield return source.Replace(" ", "_", StringComparison.Ordinal);
            yield return source.Replace("_", "", StringComparison.Ordinal);
        }
    }

    private static bool KeysMatch(string? fieldKey, string metadataKey)
    {
        if (string.IsNullOrWhiteSpace(fieldKey) || string.IsNullOrWhiteSpace(metadataKey))
            return false;

        return string.Equals(NormalizeKey(fieldKey), NormalizeKey(metadataKey), StringComparison.Ordinal);
    }

    private static string NormalizeKey(string key) =>
        new string(key.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    private static bool TryGetMetadataValue(
        IReadOnlyDictionary<string, string> metadata,
        string key,
        out string value)
    {
        value = string.Empty;
        if (string.IsNullOrWhiteSpace(key))
            return false;

        if (metadata.TryGetValue(key, out var direct) && !string.IsNullOrWhiteSpace(direct))
        {
            value = direct.Trim();
            return true;
        }

        foreach (var kv in metadata)
        {
            if (KeysMatch(key, kv.Key) && !string.IsNullOrWhiteSpace(kv.Value))
            {
                value = kv.Value.Trim();
                return true;
            }
        }

        return false;
    }
}
