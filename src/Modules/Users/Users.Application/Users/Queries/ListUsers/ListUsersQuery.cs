using MediatR;

namespace SaaSApp.Users.Application.Users.Queries.ListUsers;

/// <summary>List all users in the current tenant (excluding soft-deleted).</summary>
public record ListUsersQuery : IRequest<ListUsersQueryResult>;

/// <summary>List of users for ListUsers response.</summary>
public record ListUsersQueryResult(IReadOnlyList<ListUsersItem> Items);

/// <summary>User summary in list response.</summary>
public record ListUsersItem(Guid Id, string Email, string DisplayName, string Role, DateTime CreatedAtUtc,
    string? FirstName = null, string? LastName = null, string? AuthStrategy = null);
