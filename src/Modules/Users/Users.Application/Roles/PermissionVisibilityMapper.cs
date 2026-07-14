using SaaSApp.Users.Application.Contracts;

namespace SaaSApp.Users.Application.Roles;

/// <summary>
/// Maps granted category keys onto the full active permission catalog with visible flags.
/// </summary>
public static class PermissionVisibilityMapper
{
    public static async Task<(int PermissionCount, IReadOnlyList<PermissionKeyItem> Items)> MapAsync(
        IReadOnlyList<string> grantedStoredKeys,
        IPermissionCategoryRepository categoryRepository,
        CancellationToken cancellationToken = default)
    {
        var grantedSet = new HashSet<string>(
            PermissionCategoryResolver.NormalizeStoredKeys(grantedStoredKeys),
            StringComparer.OrdinalIgnoreCase);

        var categories = await categoryRepository.ListActiveAsync(cancellationToken);
        var items = new List<PermissionKeyItem>(categories.Count + grantedSet.Count);

        foreach (var category in categories)
        {
            items.Add(new PermissionKeyItem(
                category.Key,
                category.Name,
                grantedSet.Contains(category.Key)));
        }

        foreach (var grantedKey in grantedSet)
        {
            if (categories.Any(c => string.Equals(c.Key, grantedKey, StringComparison.OrdinalIgnoreCase)))
                continue;

            items.Add(new PermissionKeyItem(
                grantedKey,
                PermissionCategoryResolver.FormatDisplayName(grantedKey),
                Visible: true));
        }

        var permissionCount = items.Count(i => i.Visible);
        return (permissionCount, items);
    }
}
