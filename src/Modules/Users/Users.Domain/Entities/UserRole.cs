using SaaSApp.SharedKernel.Domain.Interfaces;

namespace SaaSApp.Users.Domain.Entities;

/// <summary>Many-to-many link between a custom role and a user.</summary>
public sealed class UserRole : ITenantEntity
{
    public Guid RoleId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid TenantId { get; private set; }

    public Role Role { get; private set; } = null!;

    private UserRole() { }

    private UserRole(Guid tenantId, Guid roleId, Guid userId)
    {
        TenantId = tenantId;
        RoleId = roleId;
        UserId = userId;
    }

    public static UserRole Create(Guid tenantId, Guid roleId, Guid userId) =>
        new(tenantId, roleId, userId);
}
