namespace SaaSApp.Users.Application.Contracts;

/// <summary>
/// Ensures reserved Admin / TenantUser roles exist with seeded permissions and UserRoles membership.
/// </summary>
public interface IBuiltinRoleProvisioning
{
    /// <summary>
    /// Creates Admin/TenantUser roles (with all active categories on first create) and syncs UserRoles from Users.Role.
    /// </summary>
    Task EnsureBuiltinRolesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures builtin roles exist, then attaches/detaches the user to Admin/TenantUser based on <paramref name="roleName"/>.
    /// Does not call SaveChanges — the caller's unit of work should persist.
    /// </summary>
    Task SyncUserMembershipAsync(Guid userId, string? roleName, CancellationToken cancellationToken = default);
}
