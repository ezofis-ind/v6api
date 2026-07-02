namespace SaaSApp.Users.Application.Contracts;

public sealed record PermissionCategoryItem(Guid Id, string Key, string Name, int SortOrder);

/// <summary>Reads system permission categories from the tenant database.</summary>
public interface IPermissionCategoryRepository
{
    Task<IReadOnlyList<PermissionCategoryItem>> ListActiveAsync(CancellationToken cancellationToken = default);
}
