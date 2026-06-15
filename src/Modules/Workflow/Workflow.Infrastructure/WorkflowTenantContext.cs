using SaaSApp.MultiTenancy;
using SaaSApp.Workflow.Application.Contracts;

namespace SaaSApp.Workflow.Infrastructure;

public sealed class WorkflowTenantContext : ITenantContext
{
    private readonly ITenantProvider _tenantProvider;
    private readonly ITenantConnectionProvider _connectionProvider;

    public WorkflowTenantContext(ITenantProvider tenantProvider, ITenantConnectionProvider connectionProvider)
    {
        _tenantProvider = tenantProvider;
        _connectionProvider = connectionProvider;
    }

    public Guid? TenantId => _tenantProvider.GetTenantId();
    public string? ConnectionString => _connectionProvider.ConnectionString;
}
