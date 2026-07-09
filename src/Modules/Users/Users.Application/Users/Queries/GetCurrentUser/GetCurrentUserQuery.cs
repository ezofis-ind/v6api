using MediatR;
using SaaSApp.Users.Application.Users;

namespace SaaSApp.Users.Application.Users.Queries.GetCurrentUser;

/// <summary>Load the authenticated user's row from users.Users (current tenant).</summary>
public record GetCurrentUserQuery(Guid UserId) : IRequest<UserExtendedResponse?>;
