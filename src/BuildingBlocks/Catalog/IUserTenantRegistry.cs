namespace SaaSApp.Catalog;

/// <summary>
/// Registers a user (email) as a member of a tenant with a role. Used for "my organizations" at login.
/// </summary>
public interface IUserTenantRegistry
{
    Task AddOrUpdateAsync(string email, Guid tenantId, string role, CancellationToken cancellationToken = default);
}
