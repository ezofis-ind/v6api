namespace SaaSApp.MultiTenancy;

/// <summary>
/// Resolves a tenant's database connection string from the catalog (e.g. Tenants table).
/// </summary>
public interface ITenantConnectionStringResolver
{
    /// <summary>
    /// Gets the connection string for the given tenant. Returns null if tenant not found.
    /// </summary>
    Task<string?> GetConnectionStringAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
