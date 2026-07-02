using Microsoft.EntityFrameworkCore;
using SaaSApp.Users.Application.Contracts;
using SaaSApp.Users.Domain.Entities;

namespace SaaSApp.Users.Infrastructure.Persistence;

public sealed class PermissionCategoryRepository : IPermissionCategoryRepository
{
    private readonly UsersDbContext _context;

    public PermissionCategoryRepository(UsersDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<PermissionCategoryItem>> ListActiveAsync(CancellationToken cancellationToken = default)
    {
        return await _context.PermissionCategories
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .Select(c => new PermissionCategoryItem(c.Id, c.Key, c.Name, c.SortOrder))
            .ToListAsync(cancellationToken);
    }
}
