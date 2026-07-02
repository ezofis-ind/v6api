using MediatR;

namespace SaaSApp.Users.Application.Roles.Commands.SetRoleMenus;

public record SetRoleMenusCommand(
    Guid RoleId,
    IReadOnlyList<Guid> MenuIds,
    Guid? DefaultLandingMenuId) : IRequest<SetRoleMenusCommandResult>;

public record SetRoleMenusCommandResult(
    bool Success,
    string? Error = null,
    int StatusCode = 400);
