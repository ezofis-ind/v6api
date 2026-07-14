using MediatR;
using SaaSApp.Users.Application.Contracts;
using SaaSApp.Users.Application.Roles;
using SaaSApp.Users.Application.Users;

namespace SaaSApp.Users.Application.Users.Queries.GetCurrentUser;

public sealed class GetCurrentUserQueryHandler : IRequestHandler<GetCurrentUserQuery, UserExtendedResponse?>
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

    public async Task<UserExtendedResponse?> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user == null)
            return null;

        var managerEmail = await ResolveManagerEmailAsync(user.ManagerId, cancellationToken);

        var permissionKeys = await _roleRepository.ListPermissionKeysForUserAsync(
            request.UserId,
            user.Role,
            cancellationToken);
        var (permissionCount, groupedPermissions) = await PermissionVisibilityMapper.MapAsync(
            permissionKeys,
            _categoryRepository,
            cancellationToken);

        return UserExtendedResponseMapper.MapWithPermissions(
            user,
            managerEmail,
            permissionCount,
            groupedPermissions);
    }

    private async Task<string?> ResolveManagerEmailAsync(Guid? managerId, CancellationToken cancellationToken)
    {
        if (managerId == null)
            return null;

        var manager = await _userRepository.GetByIdAsync(managerId.Value, cancellationToken);
        return manager?.Email;
    }
}
