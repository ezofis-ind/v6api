using SaaSApp.SharedKernel.Domain.Interfaces;

namespace SaaSApp.Users.Domain.Entities;

/// <summary>Permission granted to a custom role.</summary>
public sealed class RolePermission : ITenantEntity
{
    public Guid RoleId { get; private set; }
    public string PermissionKey { get; private set; } = null!;
    public Guid TenantId { get; private set; }

    public Role Role { get; private set; } = null!;

    private RolePermission() { }

    private RolePermission(Guid tenantId, Guid roleId, string permissionKey)
    {
        TenantId = tenantId;
        RoleId = roleId;
        PermissionKey = permissionKey;
    }

    public static RolePermission Create(Guid tenantId, Guid roleId, string permissionKey) =>
        new(tenantId, roleId, permissionKey.Trim());
}
