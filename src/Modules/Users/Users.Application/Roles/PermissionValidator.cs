using SaaSApp.Users.Application.Contracts;
using SaaSApp.Users.Application.Roles;

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
        var (_, invalidValue) = await PermissionCategoryResolver.ResolveAsync(
            permissionKeys,
            _categoryRepository,
            cancellationToken);

        return invalidValue;
    }
}
