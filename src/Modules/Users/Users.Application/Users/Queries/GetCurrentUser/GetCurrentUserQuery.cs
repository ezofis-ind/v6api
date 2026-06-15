using MediatR;

namespace SaaSApp.Users.Application.Users.Queries.GetCurrentUser;

/// <summary>Load the authenticated user's row from users.Users (current tenant).</summary>
public record GetCurrentUserQuery(Guid UserId) : IRequest<CurrentUserDetailResult?>;

/// <summary>Full user profile for GET /api/usersession (secrets excluded).</summary>
public record CurrentUserDetailResult(
    Guid Id,
    Guid TenantId,
    string Email,
    string DisplayName,
    string Role,
    DateTime CreatedAtUtc,
    string? FirstName,
    string? LastName,
    string? ProfileId,
    string? PhoneNo,
    string? SecondaryEmail,
    string? Language,
    string? CountryCode,
    string? Department,
    string? JobTitle,
    Guid? ManagerId,
    string? UserType,
    string? AuthStrategy,
    string? LoginType,
    string? LoginName,
    string? DeviceId,
    bool TwoFactorAuthentication,
    int? PasswordAge,
    string? GoogleSubjectId,
    string? MicrosoftOid,
    string? AvatarPath,
    string? IdCardPath,
    string? SignaturePath,
    string? UiPreference,
    DateTime? ModifiedAtUtc,
    Guid? CreatedBy,
    Guid? ModifiedBy);
