using MediatR;
using SaaSApp.Users.Application.Users;

namespace SaaSApp.Users.Application.Users.Queries.GetUserById;

/// <summary>Get a user by ID in the current tenant.</summary>
public record GetUserByIdQuery(Guid UserId) : IRequest<UserExtendedResponse?>;
