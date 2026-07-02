using SaaSApp.SharedKernel.Domain;
using SaaSApp.SharedKernel.Domain.Interfaces;

namespace SaaSApp.Users.Domain.Entities;

/// <summary>Tenant-scoped user group for organizational membership.</summary>
public sealed class Group : Entity<Guid>, ITenantEntity
{
    private readonly List<UserGroup> _userGroups = [];

    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public bool IsDeleted { get; private set; }

    public IReadOnlyCollection<UserGroup> UserGroups => _userGroups.AsReadOnly();

    private Group() { }

    private Group(Guid id, Guid tenantId, string name, string? description)
    {
        Id = id;
        TenantId = tenantId;
        Name = name;
        Description = description;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static Group Create(Guid tenantId, string name, string? description = null)
    {
        var trimmedName = name.Trim();
        return new Group(
            Guid.NewGuid(),
            tenantId,
            trimmedName,
            string.IsNullOrWhiteSpace(description) ? null : description.Trim());
    }

    public void Update(string name, string? description)
    {
        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    public void AssignUsers(IEnumerable<Guid> userIds)
    {
        foreach (var userId in userIds.Distinct())
            _userGroups.Add(UserGroup.Create(TenantId, Id, userId));
    }

    public void ReplaceUsers(IEnumerable<Guid> userIds)
    {
        _userGroups.Clear();
        AssignUsers(userIds);
    }

    public void SoftDelete() => IsDeleted = true;
}
