using MediatR;

namespace SaaSApp.Users.Application.Users.Commands.DeleteUser;

/// <summary>Soft-delete a user by ID.</summary>
public record DeleteUserCommand(Guid UserId) : IRequest<DeleteUserCommandResult>;

/// <summary>Whether the user was found and soft-deleted.</summary>
public record DeleteUserCommandResult(bool Found);
