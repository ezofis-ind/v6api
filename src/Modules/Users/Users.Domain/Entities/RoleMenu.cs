using SaaSApp.SharedKernel.Domain.Interfaces;

namespace SaaSApp.Users.Domain.Entities;

/// <summary>Many-to-many link between a custom role and a navigation menu.</summary>
public sealed class RoleMenu : ITenantEntity
{
    public Guid RoleId { get; private set; }
    public Guid MenuId { get; private set; }
    public Guid TenantId { get; private set; }
    public bool IsDefaultLanding { get; private set; }

    public Role Role { get; private set; } = null!;
    public Menu Menu { get; private set; } = null!;

    private RoleMenu() { }

    private RoleMenu(Guid tenantId, Guid roleId, Guid menuId, bool isDefaultLanding)
    {
        TenantId = tenantId;
        RoleId = roleId;
        MenuId = menuId;
        IsDefaultLanding = isDefaultLanding;
    }

    public static RoleMenu Create(Guid tenantId, Guid roleId, Guid menuId, bool isDefaultLanding = false) =>
        new(tenantId, roleId, menuId, isDefaultLanding);
}
