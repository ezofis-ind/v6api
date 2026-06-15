using MediatR;

namespace SaaSApp.Users.Application.Users.Commands.CreateUser;

/// <summary>Create a user in the current tenant. Password enables Ezofis login.</summary>
public record CreateUserCommand(string Email, string DisplayName, string? Password = null, string? Role = null, string? FirstName = null, string? LastName = null, string? AuthStrategy = null) : IRequest<CreateUserCommandResult>;

/// <summary>Result of CreateUser. Contains the new user ID.</summary>
public record CreateUserCommandResult(Guid UserId);
