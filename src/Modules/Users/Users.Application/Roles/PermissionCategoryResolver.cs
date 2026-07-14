using System.Text;
using SaaSApp.Users.Application.Contracts;

namespace SaaSApp.Users.Application.Roles;

/// <summary>
/// Resolves role permission payloads as category names/keys (not category.action keys).
/// </summary>
public static class PermissionCategoryResolver
{
    public const string DottedKeyErrorMessage =
        "Use category names only (e.g. Dashboard, workflow). Permission keys like '{0}' are not supported.";

    /// <summary>
    /// Resolve category names or keys to canonical stored category keys.
    /// Rejects dotted legacy keys. Auto-provisions unknown categories.
    /// </summary>
    public static async Task<(IReadOnlyList<string> CategoryKeys, string? Error)> ResolveAsync(
        IEnumerable<string> rawValues,
        IPermissionCategoryRepository categoryRepository,
        CancellationToken cancellationToken = default)
    {
        var normalizedInputs = NormalizeInputs(rawValues);
        if (normalizedInputs.Count == 0)
            return ([], null);

        foreach (var value in normalizedInputs)
        {
            if (value.Contains('.'))
                return ([], string.Format(DottedKeyErrorMessage, value));
        }

        var categories = await categoryRepository.ListActiveAsync(cancellationToken);
        var byKey = categories.ToDictionary(c => c.Key, c => c, StringComparer.OrdinalIgnoreCase);
        var byName = categories
            .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var resolvedKeys = new List<string>();
        var toProvision = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var value in normalizedInputs)
        {
            if (byKey.TryGetValue(value, out var byKeyMatch))
            {
                AddDistinct(resolvedKeys, byKeyMatch.Key);
                continue;
            }

            if (byName.TryGetValue(value, out var byNameMatch))
            {
                AddDistinct(resolvedKeys, byNameMatch.Key);
                continue;
            }

            var slug = ToCategoryKey(value);
            if (string.IsNullOrWhiteSpace(slug))
                return ([], $"Invalid permission category: '{value}'.");

            if (byKey.TryGetValue(slug, out var slugMatch))
            {
                AddDistinct(resolvedKeys, slugMatch.Key);
                continue;
            }

            toProvision[slug] = value;
            AddDistinct(resolvedKeys, slug);
        }

        if (toProvision.Count > 0)
        {
            await categoryRepository.EnsureCategoriesExistAsync(
                toProvision.Select(kv => (kv.Key, kv.Value)),
                cancellationToken);
        }

        return (resolvedKeys, null);
    }

    /// <summary>
    /// Normalize stored RolePermissions values to category keys (legacy category.action → category).
    /// </summary>
    public static IReadOnlyList<string> NormalizeStoredKeys(IEnumerable<string> storedKeys) =>
        storedKeys
            .Select(ExtractCategoryKey)
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static string FormatDisplayName(string key)
    {
        if (string.IsNullOrEmpty(key))
            return key;

        return key.Length == 1
            ? key.ToUpperInvariant()
            : char.ToUpperInvariant(key[0]) + key[1..];
    }

    private static IReadOnlyList<string> NormalizeInputs(IEnumerable<string> rawValues) =>
        rawValues
            .Select(v => v?.Trim() ?? string.Empty)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static string ExtractCategoryKey(string storedKey)
    {
        if (string.IsNullOrWhiteSpace(storedKey))
            return string.Empty;

        var trimmed = storedKey.Trim();
        var dotIndex = trimmed.IndexOf('.');
        var category = dotIndex > 0 ? trimmed[..dotIndex] : trimmed;
        return category.ToLowerInvariant();
    }

    private static string ToCategoryKey(string value)
    {
        var trimmed = value.Trim().ToLowerInvariant();
        var builder = new StringBuilder(trimmed.Length);
        foreach (var ch in trimmed)
        {
            if (char.IsLetterOrDigit(ch) || ch is '-' or '_')
                builder.Append(ch);
        }

        return builder.ToString();
    }

    private static void AddDistinct(List<string> keys, string key)
    {
        if (!keys.Exists(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase)))
            keys.Add(key);
    }
}
