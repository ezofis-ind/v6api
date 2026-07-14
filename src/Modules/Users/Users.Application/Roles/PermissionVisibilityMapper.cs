using SaaSApp.Users.Application.Contracts;

namespace SaaSApp.Users.Application.Roles;

public static class PermissionVisibilityMapper
{
    public static async Task<(int PermissionCount, IReadOnlyList<PermissionKeyItem> Items)> MapAsync(
        IReadOnlyList<string> grantedStoredKeys,
        IPermissionCategoryRepository categoryRepository,
        CancellationToken cancellationToken = default)
    {
        var grantedKeys = PermissionCategoryResolver.NormalizeStoredKeys(grantedStoredKeys);
        var grantedSet = new HashSet<string>(grantedKeys, StringComparer.OrdinalIgnoreCase);

        var categories = await categoryRepository.ListActiveAsync(cancellationToken);
        var catalogKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var items = new List<PermissionKeyItem>(categories.Count + grantedKeys.Count);
        foreach (var category in categories)
        {
            catalogKeys.Add(category.Key);
            items.Add(new PermissionKeyItem(
                category.Key,
                category.Name,
                grantedSet.Contains(category.Key)));
        }

        foreach (var key in grantedKeys)
        {
            if (catalogKeys.Contains(key))
                continue;

            items.Add(new PermissionKeyItem(key, FormatDisplayName(key), true));
        }

        var permissionCount = items.Count(i => i.Visible);
        return (permissionCount, items);
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
