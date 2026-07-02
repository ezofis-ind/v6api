using SaaSApp.SharedKernel.Domain;

namespace SaaSApp.Users.Domain.Entities;

/// <summary>
/// System-defined permission category (matrix row). Managed via DB seed/migrations, not by customers.
/// </summary>
public sealed class PermissionCategory : Entity<Guid>
{
    public string Key { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; }

    private PermissionCategory() { }

    public PermissionCategory(Guid id, string key, string name, int sortOrder, bool isActive = true)
    {
        Id = id;
        Key = key.Trim().ToLowerInvariant();
        Name = name.Trim();
        SortOrder = sortOrder;
        IsActive = isActive;
    }
}
