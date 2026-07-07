namespace SaaSApp.Users.Application.Contracts;

/// <summary>Idempotent users schema patches for tenant databases.</summary>
public interface IUsersSchemaEnsurer
{
    Task EnsureGroupsTablesAsync(CancellationToken cancellationToken = default);

    Task EnsurePermissionCategoriesAsync(CancellationToken cancellationToken = default);

    Task EnsureRoleMenusTablesAsync(CancellationToken cancellationToken = default);

    Task EnsureMenusTablesAsync(CancellationToken cancellationToken = default);
}
