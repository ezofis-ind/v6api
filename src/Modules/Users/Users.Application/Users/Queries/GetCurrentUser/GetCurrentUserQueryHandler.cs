using MediatR;
using SaaSApp.Users.Application.Contracts;
using SaaSApp.Users.Domain.Entities;

namespace SaaSApp.Users.Application.Users.Queries.GetCurrentUser;

public sealed class GetCurrentUserQueryHandler : IRequestHandler<GetCurrentUserQuery, CurrentUserDetailResult?>
{
    private readonly IUserRepository _userRepository;

    public GetCurrentUserQueryHandler(IUserRepository userRepository) =>
        _userRepository = userRepository;

    public async Task<CurrentUserDetailResult?> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        return user == null ? null : Map(user);
    }

    private static CurrentUserDetailResult Map(User user) =>
        new(
            user.Id,
            user.TenantId,
            user.Email,
            user.DisplayName,
            user.Role,
            user.CreatedAtUtc,
            user.FirstName,
            user.LastName,
            user.ProfileId,
            user.PhoneNo,
            user.SecondaryEmail,
            user.Language,
            user.CountryCode,
            user.Department,
            user.JobTitle,
            user.ManagerId,
            user.UserType,
            user.AuthStrategy,
            user.LoginType,
            user.LoginName,
            user.DeviceId,
            user.TwoFactorAuthentication,
            user.PasswordAge,
            user.GoogleSubjectId,
            user.MicrosoftOid,
            user.AvatarPath,
            user.IdCardPath,
            user.SignaturePath,
            user.UiPreference,
            user.ModifiedAtUtc,
            user.CreatedBy,
            user.ModifiedBy);
}
