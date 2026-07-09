using MediatR;

namespace SaaSApp.Users.Application.Users.Commands.UpdateUser;

/// <summary>Update a user. Only non-null fields are applied.</summary>
public record UpdateUserCommand(
    Guid UserId,
    string? Email = null,
    string? DisplayName = null,
    string? Password = null,
    string? Role = null,
    string? FirstName = null,
    string? LastName = null,
    string? AuthStrategy = null,
    string? UserName = null,
    string? LoginType = null,
    int? PasswordExpiryDays = null,
    DateTime? AccountExpiryDate = null,
    string? ForcePasswordResetOnLogin = null,
    string? JobTitle = null,
    string? EmployeeId = null,
    string? Department = null,
    string? BusinessUnit = null,
    string? Manager = null,
    string? Location = null,
    IReadOnlyList<string>? Groups = null,
    string? MfAuthentication = null,
    string? MfaMethods = null,
    string? PhoneNo = null,
    string? Language = null,
    string? CountryCode = null,
    string? AvatarPath = null,
    string? UiPreference = null,
    string? SecondaryEmail = null,
    string? UserType = null,
    string? IdCardPath = null,
    string? SignaturePath = null,
    Guid? ModifiedBy = null) : IRequest<UpdateUserCommandResult>;

/// <summary>Result of UpdateUser.</summary>
public record UpdateUserCommandResult(
    bool Success = true,
    bool Found = true,
    string? Error = null,
    int StatusCode = 204,
    string? RegistryEmail = null,
    string? RegistryRole = null);
