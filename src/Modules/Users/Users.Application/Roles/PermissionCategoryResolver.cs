using SaaSApp.Users.Application.Contracts;

namespace SaaSApp.Users.Application.Roles;

public static class PermissionCategoryResolver
{
    public static async Task<(IReadOnlyList<string> CategoryKeys, string? InvalidValue)> ResolveAsync(
        IEnumerable<string> rawValues,
        IPermissionCategoryRepository categoryRepository,
        CancellationToken cancellationToken = default)
    {
        var normalized = rawValues
            .Select(v => v.Trim())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalized.Count == 0)
            return (Array.Empty<string>(), null);

        foreach (var value in normalized)
        {
            if (value.Contains('.'))
                return (normalized, value);
        }

        var categories = await categoryRepository.ListActiveAsync(cancellationToken);
        var byKey = categories.ToDictionary(c => c.Key, c => c.Key, StringComparer.OrdinalIgnoreCase);
        var byName = categories.ToDictionary(c => c.Name, c => c.Key, StringComparer.OrdinalIgnoreCase);

        var resolvedKeys = new List<string>();
        var keysToEnsure = new List<string>();

        foreach (var value in normalized)
        {
            if (byKey.TryGetValue(value, out var existingKey))
            {
                resolvedKeys.Add(existingKey);
                continue;
            }

            if (byName.TryGetValue(value, out existingKey))
            {
                resolvedKeys.Add(existingKey);
                continue;
            }

            var slug = SlugifyCategoryKey(value);
            if (string.IsNullOrWhiteSpace(slug))
                return (normalized, value);

            keysToEnsure.Add(slug);
            resolvedKeys.Add(slug);
        }

        if (keysToEnsure.Count > 0)
            await categoryRepository.EnsureCategoriesExistAsync(keysToEnsure, cancellationToken);

        return (resolvedKeys.Distinct(StringComparer.OrdinalIgnoreCase).ToList(), null);
    }

    public static async Task<IReadOnlyList<string>> ToDisplayNamesAsync(
        IReadOnlyList<string> categoryKeys,
        IPermissionCategoryRepository categoryRepository,
        CancellationToken cancellationToken = default)
    {
        if (categoryKeys.Count == 0)
            return Array.Empty<string>();

        var categories = await categoryRepository.ListActiveAsync(cancellationToken);
        var byKey = categories.ToDictionary(c => c.Key, c => c.Name, StringComparer.OrdinalIgnoreCase);

        var names = new List<string>(categoryKeys.Count);
        foreach (var key in NormalizeStoredKeys(categoryKeys))
        {
            if (byKey.TryGetValue(key, out var name))
                names.Add(name);
            else
                names.Add(FormatDisplayName(key));
        }

        return names;
    }

    /// <summary>Normalize stored values to category keys (handles legacy category.action rows on read).</summary>
    public static IReadOnlyList<string> NormalizeStoredKeys(IEnumerable<string> storedValues)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in storedValues)
        {
            if (string.IsNullOrWhiteSpace(value))
                continue;

            var trimmed = value.Trim();
            var dotIndex = trimmed.IndexOf('.');
            keys.Add(dotIndex >= 0 ? trimmed[..dotIndex] : trimmed);
        }

        return keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static string SlugifyCategoryKey(string value)
    {
        var slug = value.Trim().ToLowerInvariant();
        slug = new string(slug.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_').ToArray());
        return slug;
    }

    private static string FormatDisplayName(string key)
    {
        if (string.IsNullOrEmpty(key))
            return key;

        return key.Length == 1
            ? key.ToUpperInvariant()
            : char.ToUpperInvariant(key[0]) + key[1..];
    }
}
