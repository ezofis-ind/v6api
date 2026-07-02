using Microsoft.EntityFrameworkCore;
using SaaSApp.Users.Application.Contracts;
using SaaSApp.Users.Domain.Entities;

namespace SaaSApp.Users.Infrastructure.Persistence;

public sealed class MenuRepository : IMenuRepository
{
    private readonly UsersDbContext _context;

    public MenuRepository(UsersDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Menu menu, CancellationToken cancellationToken = default)
    {
        await _context.Menus.AddAsync(menu, cancellationToken);
    }

    public async Task<Menu?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Menus.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
    }

    public async Task<MenuDetailItem?> GetDetailByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Menus
            .AsNoTracking()
            .Where(m => m.Id == id)
            .Select(m => new MenuDetailItem(
                m.Id,
                m.Key,
                m.Label,
                m.RoutePath,
                m.SortOrder,
                m.IsSystem,
                m.CreatedAtUtc))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> ExistsByKeyAsync(string key, Guid? excludeMenuId = null, CancellationToken cancellationToken = default)
    {
        var normalized = key.Trim().ToLowerInvariant();
        var query = _context.Menus.AsNoTracking().Where(m => m.Key == normalized);
        if (excludeMenuId != null)
            query = query.Where(m => m.Id != excludeMenuId.Value);

        return await query.AnyAsync(cancellationToken);
    }

    public async Task<int> CountExistingByIdsAsync(IReadOnlyList<Guid> menuIds, CancellationToken cancellationToken = default)
    {
        if (menuIds.Count == 0)
            return 0;

        return await _context.Menus
            .AsNoTracking()
            .CountAsync(m => menuIds.Contains(m.Id), cancellationToken);
    }

    public async Task<IReadOnlyList<MenuListItem>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Menus
            .AsNoTracking()
            .OrderBy(m => m.SortOrder)
            .ThenBy(m => m.Label)
            .Select(m => new MenuListItem(
                m.Id,
                m.Key,
                m.Label,
                m.RoutePath,
                m.SortOrder,
                m.IsSystem,
                m.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RoleMenuItem>> ListMenusForRoleAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        return await _context.RoleMenus
            .AsNoTracking()
            .Where(rm => rm.RoleId == roleId)
            .Join(
                _context.Menus,
                rm => rm.MenuId,
                m => m.Id,
                (rm, m) => new { rm.IsDefaultLanding, Menu = m })
            .OrderBy(x => x.Menu.SortOrder)
            .ThenBy(x => x.Menu.Label)
            .Select(x => new RoleMenuItem(
                x.Menu.Id,
                x.Menu.Key,
                x.Menu.Label,
                x.Menu.RoutePath,
                x.Menu.SortOrder,
                x.IsDefaultLanding))
            .ToListAsync(cancellationToken);
    }
}
