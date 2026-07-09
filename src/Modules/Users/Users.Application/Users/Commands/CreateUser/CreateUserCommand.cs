using MediatR;

namespace SaaSApp.Users.Application.Users.Commands.CreateUser;

/// <summary>Create a user in the current tenant.</summary>
public record CreateUserCommand(
    string Email,
    string DisplayName,
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
    Guid? CreatedBy = null) : IRequest<CreateUserCommandResult>;

/// <summary>Result of CreateUser.</summary>
public record CreateUserCommandResult(
    bool Success,
    Guid? UserId = null,
    string? RoleName = null,
    string? Error = null,
    int StatusCode = 400);
