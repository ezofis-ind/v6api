using MediatR;
using SaaSApp.Users.Application.Contracts;

namespace SaaSApp.Users.Application.Roles.Queries.GetRoleMenus;

public record GetRoleMenusQuery(Guid RoleId) : IRequest<GetRoleMenusQueryResult?>;

public record GetRoleMenusQueryResult(
    Guid RoleId,
    Guid? DefaultLandingMenuId,
    IReadOnlyList<RoleMenuItem> Menus);

public sealed class GetRoleMenusQueryHandler : IRequestHandler<GetRoleMenusQuery, GetRoleMenusQueryResult?>
{
    private readonly IRoleRepository _roleRepository;
    private readonly IMenuRepository _menuRepository;

    public GetRoleMenusQueryHandler(IRoleRepository roleRepository, IMenuRepository menuRepository)
    {
        _roleRepository = roleRepository;
        _menuRepository = menuRepository;
    }

    public async Task<GetRoleMenusQueryResult?> Handle(GetRoleMenusQuery request, CancellationToken cancellationToken)
    {
        var role = await _roleRepository.GetByIdAsync(request.RoleId, cancellationToken);
        if (role == null)
            return null;

        var menus = await _menuRepository.ListMenusForRoleAsync(request.RoleId, cancellationToken);
        var defaultLandingMenuId = menus.FirstOrDefault(m => m.IsDefaultLanding)?.Id;

        return new GetRoleMenusQueryResult(request.RoleId, defaultLandingMenuId, menus);
    }
}
