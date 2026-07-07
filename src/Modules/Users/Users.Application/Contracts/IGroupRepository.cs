using SaaSApp.Users.Domain.Entities;

namespace SaaSApp.Users.Application.Contracts;

public sealed record GroupListItem(Guid Id, string Name, string? Description, int UserCount, DateTime CreatedAtUtc);

public sealed record GroupMemberItem(Guid Id, string Email, string DisplayName);

public sealed record GroupDetailItem(
    Guid Id,
    string Name,
    string? Description,
    DateTime CreatedAtUtc,
    IReadOnlyList<GroupMemberItem> Users);

/// <summary>User group persistence for the current tenant.</summary>
public interface IGroupRepository
{
    Task AddAsync(Group group, CancellationToken cancellationToken = default);

    Task<Group?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<GroupDetailItem?> GetDetailByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> ExistsByNameAsync(string name, Guid? excludeGroupId = null, CancellationToken cancellationToken = default);

    Task<Group?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    Task AddMemberAsync(Guid groupId, Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GroupListItem>> ListAsync(CancellationToken cancellationToken = default);
}
