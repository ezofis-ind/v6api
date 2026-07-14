using SaaSApp.Users.Application.Contracts;

namespace SaaSApp.Users.Application.Roles;

public sealed class PermissionValidator : IPermissionValidator
{
    private readonly IPermissionCategoryRepository _categoryRepository;

    public PermissionValidator(IPermissionCategoryRepository categoryRepository)
    {
        _categoryRepository = categoryRepository;
    }

    public async Task<string?> GetFirstInvalidKeyAsync(IReadOnlyList<string> permissionKeys, CancellationToken cancellationToken = default)
    {
        var activeCategories = await _categoryRepository.ListActiveAsync(cancellationToken);
        var activeCategoryKeys = new HashSet<string>(
            activeCategories.Select(c => c.Key),
            StringComparer.OrdinalIgnoreCase);
        var activeCategoryNames = new HashSet<string>(
            activeCategories.Select(c => c.Name),
            StringComparer.OrdinalIgnoreCase);

        foreach (var key in permissionKeys)
        {
            if (string.IsNullOrWhiteSpace(key))
                continue;

            var trimmed = key.Trim();
            if (trimmed.Contains('.'))
                return key;

            if (!activeCategoryKeys.Contains(trimmed) && !activeCategoryNames.Contains(trimmed))
                return key;
        }

        return null;
    }
}
