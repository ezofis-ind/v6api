using SaaSApp.Users.Domain.Entities;

namespace SaaSApp.Users.Application.Contracts;

public sealed record RoleListItem(Guid Id, string Name, string? Description, int UserCount, int PermissionCount);

public sealed record RoleDetailItem(
    Guid Id,
    string Name,
    string? Description,
    DateTime CreatedAtUtc,
    IReadOnlyList<Guid> UserIds,
    IReadOnlyList<string> PermissionKeys);

/// <summary>Custom role persistence for the current tenant.</summary>
public interface IRoleRepository
{
    Task AddAsync(Role role, CancellationToken cancellationToken = default);

    Task<bool> ExistsByNameAsync(string name, Guid? excludeRoleId = null, CancellationToken cancellationToken = default);

    Task<Role?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<RoleDetailItem?> GetDetailByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RoleListItem>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Distinct permission keys for a user: union of UserRoles assignments and
    /// permissions where users.Roles.Name matches <paramref name="userRoleName"/> (users.Users.Role).
    /// </summary>
    Task<IReadOnlyList<string>> ListPermissionKeysForUserAsync(
        Guid userId,
        string? userRoleName,
        CancellationToken cancellationToken = default);
}
