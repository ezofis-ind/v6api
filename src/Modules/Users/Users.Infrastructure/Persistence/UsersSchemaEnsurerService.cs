using SaaSApp.Users.Application.Contracts;

namespace SaaSApp.Users.Infrastructure.Persistence;

public sealed class UsersSchemaEnsurerService : IUsersSchemaEnsurer
{
    private readonly UsersDbContext _context;

    public UsersSchemaEnsurerService(UsersDbContext context)
    {
        _context = context;
    }

    public Task EnsureGroupsTablesAsync(CancellationToken cancellationToken = default) =>
        UsersSchemaEnsurer.EnsureGroupsTablesAsync(_context, cancellationToken);

    public Task EnsurePermissionCategoriesAsync(CancellationToken cancellationToken = default) =>
        UsersSchemaEnsurer.EnsurePermissionCategoriesAsync(_context, cancellationToken);

    public Task EnsureRoleMenusTablesAsync(CancellationToken cancellationToken = default) =>
        UsersSchemaEnsurer.EnsureRoleMenusTablesAsync(_context, cancellationToken);

    public Task EnsureMenusTablesAsync(CancellationToken cancellationToken = default) =>
        UsersSchemaEnsurer.EnsureMenusTablesAsync(_context, cancellationToken);
}
