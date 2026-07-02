using SaaSApp.Users.Application.Contracts;
using SaaSApp.Users.Application.Roles.Queries.ListPermissionCatalog;
using SaaSApp.Users.Domain;

namespace SaaSApp.Users.Application.Users;

public static class UserPermissionMapper
{
    public static async Task<(int PermissionCount, IReadOnlyList<PermissionCategoryRow> PermissionKeys)> MapGroupedAsync(
        IReadOnlyList<string> permissionKeys,
        IPermissionCategoryRepository categoryRepository,
        CancellationToken cancellationToken)
    {
        var flat = MapFlat(permissionKeys);
        var categories = await categoryRepository.ListActiveAsync(cancellationToken);

        var grantedByCategory = new Dictionary<string, List<PermissionMatrixItem>>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in flat)
        {
            if (!PermissionKeyHelper.TryParse(item.Key, out var categoryKey, out _))
                continue;

            if (!grantedByCategory.TryGetValue(categoryKey, out var list))
            {
                list = [];
                grantedByCategory[categoryKey] = list;
            }

            list.Add(item);
        }

        var grouped = new List<PermissionCategoryRow>();
        foreach (var category in categories)
        {
            if (!grantedByCategory.TryGetValue(category.Key, out var items))
                continue;

            grouped.Add(new PermissionCategoryRow(
                category.Key,
                category.Name,
                items.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase).ToList()));
        }

        foreach (var (categoryKey, items) in grantedByCategory)
        {
            if (categories.Any(c => string.Equals(c.Key, categoryKey, StringComparison.OrdinalIgnoreCase)))
                continue;

            grouped.Add(new PermissionCategoryRow(
                categoryKey,
                categoryKey,
                items.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase).ToList()));
        }

        return (flat.Count, grouped);
    }

    private static IReadOnlyList<PermissionMatrixItem> MapFlat(IReadOnlyList<string> permissionKeys)
    {
        var actionLabels = PermissionActions.AllActions.ToDictionary(
            a => a.Key,
            a => a.Label,
            StringComparer.OrdinalIgnoreCase);

        var permissions = new List<PermissionMatrixItem>(permissionKeys.Count);
        foreach (var key in permissionKeys)
        {
            if (!PermissionKeyHelper.TryParse(key, out _, out var actionKey))
                continue;

            actionLabels.TryGetValue(actionKey, out var actionLabel);
            permissions.Add(new PermissionMatrixItem(key, actionKey, actionLabel ?? actionKey));
        }

        return permissions;
    }
}
