using Microsoft.EntityFrameworkCore;
using SaaSApp.MultiTenancy;
using SaaSApp.Users.Application.Contracts;
using SaaSApp.Users.Domain.Entities;

namespace SaaSApp.Users.Infrastructure.Persistence;

public sealed class GroupRepository : IGroupRepository
{
    private readonly UsersDbContext _context;
    private readonly ITenantProvider _tenantProvider;

    public GroupRepository(UsersDbContext context, ITenantProvider tenantProvider)
    {
        _context = context;
        _tenantProvider = tenantProvider;
    }

    public async Task AddAsync(Group group, CancellationToken cancellationToken = default)
    {
        await _context.Groups.AddAsync(group, cancellationToken);
    }

    public async Task<Group?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Groups
            .Include(g => g.UserGroups)
            .FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
    }

    public async Task<GroupDetailItem?> GetDetailByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var group = await _context.Groups
            .AsNoTracking()
            .Where(g => g.Id == id)
            .Select(g => new
            {
                g.Id,
                g.Name,
                g.Description,
                g.CreatedAtUtc,
                Members = g.UserGroups
                    .Join(
                        _context.Users,
                        ug => ug.UserId,
                        u => u.Id,
                        (_, u) => u)
                    .OrderBy(u => u.DisplayName)
                    .Select(u => new GroupMemberItem(u.Id, u.Email, u.DisplayName))
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (group == null)
            return null;

        return new GroupDetailItem(
            group.Id,
            group.Name,
            group.Description,
            group.CreatedAtUtc,
            group.Members);
    }

    public async Task<bool> ExistsByNameAsync(string name, Guid? excludeGroupId = null, CancellationToken cancellationToken = default)
    {
        var trimmed = name.Trim();
        var query = _context.Groups.AsNoTracking().Where(g => g.Name.Trim() == trimmed);
        if (excludeGroupId != null)
            query = query.Where(g => g.Id != excludeGroupId.Value);

        return await query.AnyAsync(cancellationToken);
    }

    public async Task<Group?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var trimmed = name.Trim();
        return await _context.Groups
            .FirstOrDefaultAsync(g => g.Name.Trim() == trimmed, cancellationToken);
    }

    public async Task AddMemberAsync(Guid groupId, Guid userId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId()
            ?? throw new InvalidOperationException("TenantId is required to add a group member.");

        var alreadyMember = await _context.UserGroups
            .AnyAsync(ug => ug.GroupId == groupId && ug.UserId == userId, cancellationToken);
        if (alreadyMember)
            return;

        await _context.UserGroups.AddAsync(
            UserGroup.Create(tenantId, groupId, userId),
            cancellationToken);
    }

    public async Task<IReadOnlyList<GroupListItem>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Groups
            .AsNoTracking()
            .OrderBy(g => g.Name)
            .Select(g => new GroupListItem(
                g.Id,
                g.Name,
                g.Description,
                g.UserGroups.Count,
                g.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }
}
