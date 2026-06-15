using MediatR;

namespace SaaSApp.Users.Application.Users.Queries.GetUserById;

/// <summary>Get a user by ID in the current tenant.</summary>
public record GetUserByIdQuery(Guid UserId) : IRequest<GetUserByIdQueryResult?>;

/// <summary>User profile for GetById response.</summary>
public record GetUserByIdQueryResult(Guid Id, string Email, string DisplayName, string Role, DateTime CreatedAtUtc,
    string? FirstName = null, string? LastName = null, string? PhoneNo = null, string? AuthStrategy = null,
    string? Department = null, string? JobTitle = null, string? Language = null, string? CountryCode = null,
    string? AvatarPath = null, string? UiPreference = null);
