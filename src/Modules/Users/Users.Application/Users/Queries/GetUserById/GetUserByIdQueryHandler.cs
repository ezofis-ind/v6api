using MediatR;
using SaaSApp.Users.Application.Contracts;

namespace SaaSApp.Users.Application.Users.Queries.GetUserById;

public sealed class GetUserByIdQueryHandler : IRequestHandler<GetUserByIdQuery, GetUserByIdQueryResult?>
{
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IPermissionCategoryRepository _categoryRepository;

    public GetUserByIdQueryHandler(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IPermissionCategoryRepository categoryRepository)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _categoryRepository = categoryRepository;
    }

    public async Task<GetUserByIdQueryResult?> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
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

        return new GetUserByIdQueryResult(
            user.Id,
            user.Email,
            user.DisplayName,
            user.Role,
            user.CreatedAtUtc,
            user.FirstName,
            user.LastName,
            user.PhoneNo,
            user.AuthStrategy,
            user.Department,
            user.JobTitle,
            user.Language,
            user.CountryCode,
            user.AvatarPath,
            user.UiPreference,
            permissionCount,
            groupedPermissions);
    }
}
