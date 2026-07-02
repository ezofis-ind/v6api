using SaaSApp.Users.Domain.Entities;

namespace SaaSApp.Users.Application.Contracts;

public sealed record MenuListItem(
    Guid Id,
    string Key,
    string Label,
    string RoutePath,
    int SortOrder,
    bool IsSystem,
    DateTime CreatedAtUtc);

public sealed record MenuDetailItem(
    Guid Id,
    string Key,
    string Label,
    string RoutePath,
    int SortOrder,
    bool IsSystem,
    DateTime CreatedAtUtc);

public sealed record RoleMenuItem(
    Guid Id,
    string Key,
    string Label,
    string RoutePath,
    int SortOrder,
    bool IsDefaultLanding);

/// <summary>Navigation menu persistence for the current tenant database.</summary>
public interface IMenuRepository
{
    Task AddAsync(Menu menu, CancellationToken cancellationToken = default);

    Task<Menu?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<MenuDetailItem?> GetDetailByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> ExistsByKeyAsync(string key, Guid? excludeMenuId = null, CancellationToken cancellationToken = default);

    Task<int> CountExistingByIdsAsync(IReadOnlyList<Guid> menuIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MenuListItem>> ListAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RoleMenuItem>> ListMenusForRoleAsync(Guid roleId, CancellationToken cancellationToken = default);
}
