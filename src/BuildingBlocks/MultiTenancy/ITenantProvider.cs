namespace SaaSApp.MultiTenancy;

/// <summary>
/// Provides the current tenant context (e.g. from JWT claim or header).
/// </summary>
public interface ITenantProvider
{
    /// <summary>
    /// Gets the current tenant ID. Returns null when tenant cannot be resolved (e.g. unauthenticated).
    /// </summary>
    Guid? GetTenantId();
}
