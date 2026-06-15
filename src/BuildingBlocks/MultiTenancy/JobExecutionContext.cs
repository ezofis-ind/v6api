namespace SaaSApp.MultiTenancy;

/// <summary>
/// Scoped ambient context for background jobs (no HTTP). When active, tenant/user providers read from here.
/// </summary>
public sealed class JobExecutionContext
{
    public Guid? TenantId { get; private set; }
    public Guid? UserId { get; private set; }

    public bool IsActive => TenantId.HasValue;

    public void Set(Guid tenantId, Guid userId)
    {
        TenantId = tenantId;
        UserId = userId;
    }

    public void Clear()
    {
        TenantId = null;
        UserId = null;
    }
}
