using SaaSApp.SharedKernel.Domain.Interfaces;

namespace SaaSApp.Users.Domain.Entities;

/// <summary>Many-to-many link between a user group and a user.</summary>
public sealed class UserGroup : ITenantEntity
{
    public Guid GroupId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid TenantId { get; private set; }

    public Group Group { get; private set; } = null!;

    private UserGroup() { }

    private UserGroup(Guid tenantId, Guid groupId, Guid userId)
    {
        TenantId = tenantId;
        GroupId = groupId;
        UserId = userId;
    }

    public static UserGroup Create(Guid tenantId, Guid groupId, Guid userId) =>
        new(tenantId, groupId, userId);
}
