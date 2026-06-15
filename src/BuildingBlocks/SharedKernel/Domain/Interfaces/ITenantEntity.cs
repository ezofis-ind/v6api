namespace SaaSApp.SharedKernel.Domain.Interfaces;

/// <summary>
/// Marks an entity as tenant-scoped. Used for global query filters.
/// </summary>
public interface ITenantEntity
{
    Guid TenantId { get; }
}
