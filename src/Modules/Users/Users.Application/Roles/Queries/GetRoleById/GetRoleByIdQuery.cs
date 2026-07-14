using MediatR;
using SaaSApp.Users.Application.Contracts;
using SaaSApp.Users.Application.Roles;

namespace SaaSApp.Users.Application.Roles.Queries.GetRoleById;

public record GetRoleByIdQuery(Guid RoleId) : IRequest<GetRoleByIdQueryResult?>;

public record GetRoleByIdQueryResult(
    Guid Id,
    string RoleName,
    string? Description,
    DateTime CreatedAtUtc,
    IReadOnlyList<Guid> Users,
    IReadOnlyList<PermissionKeyItem> PermissionKeys);

public sealed class GetRoleByIdQueryHandler : IRequestHandler<GetRoleByIdQuery, GetRoleByIdQueryResult?>
{
    private readonly IRoleRepository _roleRepository;
    private readonly IPermissionCategoryRepository _categoryRepository;

    public GetRoleByIdQueryHandler(
        IRoleRepository roleRepository,
        IPermissionCategoryRepository categoryRepository)
    {
        _roleRepository = roleRepository;
        _categoryRepository = categoryRepository;
    }

    public async Task<GetRoleByIdQueryResult?> Handle(GetRoleByIdQuery request, CancellationToken cancellationToken)
    {
        var role = await _roleRepository.GetDetailByIdAsync(request.RoleId, cancellationToken);
        if (role == null)
            return null;

        var (_, permissionKeys) = await PermissionVisibilityMapper.MapAsync(
            role.PermissionKeys,
            _categoryRepository,
            cancellationToken);

        return new GetRoleByIdQueryResult(
            role.Id,
            role.Name,
            role.Description,
            role.CreatedAtUtc,
            role.UserIds,
            permissionKeys);
    }
}
