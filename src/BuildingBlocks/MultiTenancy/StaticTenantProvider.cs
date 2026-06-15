namespace SaaSApp.MultiTenancy;

/// <summary>
/// Returns a fixed tenant ID (e.g. for running migrations during signup when there is no HTTP context).
/// </summary>
public sealed class StaticTenantProvider : ITenantProvider
{
    private readonly Guid? _tenantId;

    public StaticTenantProvider(Guid? tenantId)
    {
        _tenantId = tenantId;
    }

    public Guid? GetTenantId() => _tenantId;
}
