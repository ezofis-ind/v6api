namespace SaaSApp.Users.Application.Contracts;

/// <summary>Validates permission keys against DB categories and fixed action types.</summary>
public interface IPermissionValidator
{
    Task<string?> GetFirstInvalidKeyAsync(IReadOnlyList<string> permissionKeys, CancellationToken cancellationToken = default);
}
