using MediatR;
using SaaSApp.Users.Application.Users;

namespace SaaSApp.Users.Application.Users.Queries.ListUsers;

/// <summary>List all users in the current tenant (excluding soft-deleted).</summary>
public record ListUsersQuery : IRequest<ListUsersQueryResult>;

/// <summary>List of users for ListUsers response.</summary>
public record ListUsersQueryResult(IReadOnlyList<UserExtendedResponse> Items);
