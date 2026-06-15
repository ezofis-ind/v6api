using SaaSApp.MultiTenancy;

namespace SaaSApp.Users.Application;

internal sealed class TenantContext : Contracts.ITenantContext
{
    private readonly ITenantProvider _tenantProvider;

    public TenantContext(ITenantProvider tenantProvider)
    {
        _tenantProvider = tenantProvider;
    }

    public Guid? TenantId => _tenantProvider.GetTenantId();
}
