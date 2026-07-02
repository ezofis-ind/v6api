using Microsoft.EntityFrameworkCore;
using SaaSApp.Users.Application.Contracts;
using SaaSApp.Users.Domain.Entities;

namespace SaaSApp.Users.Infrastructure.Persistence;

public sealed class UserRepository : IUserRepository
{
    private readonly UsersDbContext _context;

    public UserRepository(UsersDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        await _context.Users.AddAsync(user, cancellationToken);
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        return await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email.Trim(), cancellationToken);
    }

    public async Task<IReadOnlyList<User>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .AsNoTracking()
            .OrderBy(u => u.DisplayName)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountExistingByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
            return 0;

        return await _context.Users
            .AsNoTracking()
            .CountAsync(u => ids.Contains(u.Id), cancellationToken);
    }

    public async Task<IReadOnlyList<string>> RenameRoleForUsersAsync(
        string oldRoleName,
        string newRoleName,
        CancellationToken cancellationToken = default)
    {
        var oldNormalized = oldRoleName.Trim().ToLowerInvariant();
        var users = await _context.Users
            .Where(u => u.Role.ToLower() == oldNormalized)
            .ToListAsync(cancellationToken);

        foreach (var user in users)
            user.Update(role: newRoleName);

        return users.Select(u => u.Email).ToList();
    }

    public void Update(User user)
    {
        _context.Users.Update(user);
    }

    public void Delete(User user)
    {
        _context.Users.Remove(user);
    }
}
