using SaaSApp.Users.Application.Contracts;
using SaaSApp.Users.Domain;

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

        foreach (var key in permissionKeys)
        {
            if (!PermissionKeyHelper.TryParse(key, out var categoryKey, out _))
                return key;

            if (!activeCategoryKeys.Contains(categoryKey))
                return key;
        }

        return null;
    }
}
