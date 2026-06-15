namespace SaaSApp.Users.Application.Contracts;

/// <summary>
/// Application-level tenant context (resolved from ITenantProvider).
/// </summary>
public interface ITenantContext
{
    Guid? TenantId { get; }
}
