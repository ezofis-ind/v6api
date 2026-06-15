namespace SaaSApp.MultiTenancy;

/// <summary>
/// Scoped holder for the current request's tenant connection string (set by middleware).
/// </summary>
public sealed class TenantConnectionProvider : ITenantConnectionProvider
{
    public string? ConnectionString { get; private set; }

    public void SetConnectionString(string connectionString)
    {
        ConnectionString = connectionString;
    }
}
