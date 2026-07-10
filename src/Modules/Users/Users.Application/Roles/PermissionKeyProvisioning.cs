using SaaSApp.Users.Application.Contracts;
using SaaSApp.Users.Domain;

namespace SaaSApp.Users.Application.Roles;

public static class PermissionKeyProvisioning
{
    public static async Task<(IReadOnlyList<string> Keys, string? InvalidKey)> PrepareAsync(
        IEnumerable<string> rawKeys,
        IPermissionCategoryRepository categoryRepository,
        CancellationToken cancellationToken = default)
    {
        var permissionKeys = PermissionKeyHelper.NormalizeKeys(rawKeys);
        if (permissionKeys.Count == 0)
            return (permissionKeys, null);

        foreach (var key in permissionKeys)
        {
            if (!PermissionKeyHelper.TryParse(key, out _, out _))
                return (permissionKeys, key);
        }

        var categoryKeys = ExtractCategoryKeys(permissionKeys);
        await categoryRepository.EnsureCategoriesExistAsync(categoryKeys, cancellationToken);

        return (permissionKeys, null);
    }

    private static IReadOnlyList<string> ExtractCategoryKeys(IReadOnlyList<string> permissionKeys)
    {
        var categories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in permissionKeys)
        {
            if (PermissionKeyHelper.TryParse(key, out var categoryKey, out _))
                categories.Add(categoryKey);
        }

        return categories.ToList();
    }
}
