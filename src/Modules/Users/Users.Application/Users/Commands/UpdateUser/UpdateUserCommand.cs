using MediatR;

namespace SaaSApp.Users.Application.Users.Commands.UpdateUser;

/// <summary>Update a user. Only non-null fields are applied.</summary>
public record UpdateUserCommand(Guid UserId, string? DisplayName = null, string? Role = null,
    string? FirstName = null, string? LastName = null, string? PhoneNo = null, string? Department = null,
    string? JobTitle = null, string? Language = null, string? CountryCode = null, string? AvatarPath = null,
    string? UiPreference = null) : IRequest<UpdateUserCommandResult>;
/// <summary>Whether the user was found and updated.</summary>
public record UpdateUserCommandResult(bool Found);
