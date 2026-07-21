namespace SaaSApp.MultiTenancy;

/// <summary>Resolves a short display label for Hangfire / logs (tenant name preferred).</summary>
public interface ITenantDisplayResolver
{
    /// <summary>
    /// Returns catalog tenant name, or the tenant id GUID string if the tenant is missing/unnamed.
    /// </summary>
    Task<string> ResolveAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
