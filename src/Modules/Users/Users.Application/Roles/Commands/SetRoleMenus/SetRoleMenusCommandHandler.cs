using MediatR;
using SaaSApp.Users.Application.Contracts;

namespace SaaSApp.Users.Application.Roles.Commands.SetRoleMenus;

public sealed class SetRoleMenusCommandHandler : IRequestHandler<SetRoleMenusCommand, SetRoleMenusCommandResult>
{
    private readonly IRoleRepository _roleRepository;
    private readonly IMenuRepository _menuRepository;

    public SetRoleMenusCommandHandler(IRoleRepository roleRepository, IMenuRepository menuRepository)
    {
        _roleRepository = roleRepository;
        _menuRepository = menuRepository;
    }

    public async Task<SetRoleMenusCommandResult> Handle(SetRoleMenusCommand request, CancellationToken cancellationToken)
    {
        var menuIds = (request.MenuIds ?? []).Distinct().ToList();

        if (menuIds.Count == 0)
            return Fail("At least one menu is required.");

        if (request.DefaultLandingMenuId != null && !menuIds.Contains(request.DefaultLandingMenuId.Value))
            return Fail("Default landing menu must be included in the assigned menus.");

        var role = await _roleRepository.GetByIdAsync(request.RoleId, cancellationToken);
        if (role == null)
            return Fail("Role not found.", 404);

        var existingMenuCount = await _menuRepository.CountExistingByIdsAsync(menuIds, cancellationToken);
        if (existingMenuCount != menuIds.Count)
            return Fail("One or more menus were not found.", 404);

        var defaultLandingMenuId = request.DefaultLandingMenuId ?? menuIds[0];
        var assignments = menuIds
            .Select(menuId => (MenuId: menuId, IsDefaultLanding: menuId == defaultLandingMenuId))
            .ToList();

        role.ReplaceMenus(assignments);
        return new SetRoleMenusCommandResult(Success: true, StatusCode: 204);
    }

    private static SetRoleMenusCommandResult Fail(string error, int statusCode = 400) =>
        new(Success: false, Error: error, StatusCode: statusCode);
}
