namespace SaaSApp.MultiTenancy;

/// <summary>
/// Provides the current request's tenant database connection string (database-per-tenant).
/// Set by middleware after resolving tenant from JWT and looking up connection in catalog.
/// </summary>
public interface ITenantConnectionProvider
{
    /// <summary>
    /// The connection string for the current tenant's database. Null if not yet resolved.
    /// </summary>
    string? ConnectionString { get; }

    /// <summary>
    /// Sets the connection string for the current request. Called by tenant resolution middleware.
    /// </summary>
    void SetConnectionString(string connectionString);
}
