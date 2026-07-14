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

    public async Task EnsureCategoriesExistAsync(
        IEnumerable<(string Key, string Name)> categories,
        CancellationToken cancellationToken = default)
    {
        var normalized = categories
            .Select(c => (
                Key: c.Key.Trim().ToLowerInvariant(),
                Name: string.IsNullOrWhiteSpace(c.Name) ? FormatCategoryName(c.Key) : c.Name.Trim()))
            .Where(c => !string.IsNullOrWhiteSpace(c.Key))
            .GroupBy(c => c.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        if (normalized.Count == 0)
            return;

        var existingKeys = await _context.PermissionCategories
            .AsNoTracking()
            .Select(c => c.Key)
            .ToListAsync(cancellationToken);

        var existingSet = new HashSet<string>(existingKeys, StringComparer.OrdinalIgnoreCase);
        var missing = normalized.Where(c => !existingSet.Contains(c.Key)).ToList();
        if (missing.Count == 0)
            return;

        var maxSortOrder = await _context.PermissionCategories
            .MaxAsync(c => (int?)c.SortOrder, cancellationToken) ?? 0;

        foreach (var (key, name) in missing)
        {
            maxSortOrder++;
            var category = PermissionCategory.Create(key, name, maxSortOrder);
            await _context.PermissionCategories.AddAsync(category, cancellationToken);
        }
    }

    private static string FormatCategoryName(string key)
    {
        if (string.IsNullOrEmpty(key))
            return key;

        return key.Length == 1
            ? key.ToUpperInvariant()
            : char.ToUpperInvariant(key[0]) + key[1..];
    }
}
