namespace SaaSApp.Users.Application.Contracts;

/// <summary>Validates permission keys against active categories in the tenant database.</summary>
public interface IPermissionValidator
{
    Task<string?> GetFirstInvalidKeyAsync(IReadOnlyList<string> permissionKeys, CancellationToken cancellationToken = default);
}
