using MediatR;
using SaaSApp.Users.Application.Contracts;
using SaaSApp.Users.Application.Roles.Queries.ListPermissionCatalog;
using SaaSApp.Users.Domain.Entities;

namespace SaaSApp.Users.Application.Users.Queries.GetCurrentUser;

public sealed class GetCurrentUserQueryHandler : IRequestHandler<GetCurrentUserQuery, CurrentUserDetailResult?>
{
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IPermissionCategoryRepository _categoryRepository;

    public GetCurrentUserQueryHandler(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IPermissionCategoryRepository categoryRepository)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _categoryRepository = categoryRepository;
    }

    public async Task<CurrentUserDetailResult?> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user == null)
            return null;

        var permissionKeys = await _roleRepository.ListPermissionKeysForUserAsync(
            request.UserId,
            user.Role,
            cancellationToken);
        var (permissionCount, groupedPermissions) = await UserPermissionMapper.MapGroupedAsync(
            permissionKeys,
            _categoryRepository,
            cancellationToken);

        return Map(user, permissionCount, groupedPermissions);
    }

    private static CurrentUserDetailResult Map(
        User user,
        int permissionCount,
        IReadOnlyList<PermissionCategoryRow> permissionKeys) =>
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
            user.ModifiedBy,
            permissionCount,
            permissionKeys);
}
