using SaaSApp.SharedKernel.Domain;

namespace SaaSApp.Users.Domain.Entities;

/// <summary>Navigation menu item for role-based UI access.</summary>
public sealed class Menu : Entity<Guid>
{
    public string Key { get; private set; } = null!;
    public string Label { get; private set; } = null!;
    public string RoutePath { get; private set; } = null!;
    public int SortOrder { get; private set; }
    public bool IsSystem { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private Menu() { }

    public Menu(Guid id, string key, string label, string routePath, int sortOrder, bool isSystem = false)
    {
        Id = id;
        Key = key.Trim().ToLowerInvariant();
        Label = label.Trim();
        RoutePath = routePath.Trim();
        SortOrder = sortOrder;
        IsSystem = isSystem;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static Menu Create(string key, string label, string routePath, int sortOrder) =>
        new(Guid.NewGuid(), key, label, routePath, sortOrder, isSystem: false);

    public void Update(string label, string routePath, int sortOrder)
    {
        Label = label.Trim();
        RoutePath = routePath.Trim();
        SortOrder = sortOrder;
    }

    public void SoftDelete() => IsDeleted = true;
}
