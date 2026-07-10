namespace SaaSApp.Users.Application.Contracts;

public sealed record PermissionCategoryItem(Guid Id, string Key, string Name, int SortOrder);

/// <summary>Reads and provisions permission categories in the tenant database.</summary>
public interface IPermissionCategoryRepository
{
    Task<IReadOnlyList<PermissionCategoryItem>> ListActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>Insert any category keys that do not already exist.</summary>
    Task EnsureCategoriesExistAsync(IEnumerable<string> categoryKeys, CancellationToken cancellationToken = default);
}
