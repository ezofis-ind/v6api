namespace SaaSApp.Users.Application.Contracts;

/// <summary>Syncs user role name changes to the catalog UserTenants table.</summary>
public interface IUserTenantRoleSync
{
    Task SyncRoleForUserAsync(string email, Guid tenantId, string role, CancellationToken cancellationToken = default);
}
