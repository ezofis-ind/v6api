namespace SaaSApp.Users.Domain;

/// <summary>Builds and parses permission keys in the form {categoryKey}.{actionKey}.</summary>
public static class PermissionKeyHelper
{
    public static string Build(string categoryKey, string actionKey) =>
        $"{categoryKey.Trim()}.{actionKey.Trim()}".ToLowerInvariant();

    public static bool TryParse(string permissionKey, out string categoryKey, out string actionKey)
    {
        categoryKey = string.Empty;
        actionKey = string.Empty;

        if (string.IsNullOrWhiteSpace(permissionKey))
            return false;

        var parts = permissionKey.Trim().Split('.', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
            return false;

        categoryKey = parts[0];
        actionKey = parts[1];
        return true;
    }

    public static IReadOnlyList<string> NormalizeKeys(IEnumerable<string> keys) =>
        keys.Select(k => k.Trim().ToLowerInvariant())
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
}
