using MediatR;

namespace SaaSApp.Users.Application.Roles.Commands.CreateRole;

public record CreateRoleCommand(
    string Name,
    string? Description,
    IReadOnlyList<Guid> UserIds,
    IReadOnlyList<string> PermissionKeys) : IRequest<CreateRoleCommandResult>;

public record CreateRoleCommandResult(
    bool Success,
    Guid? RoleId = null,
    string? RoleName = null,
    int UserCount = 0,
    int PermissionCount = 0,
    string? Error = null,
    int StatusCode = 400);
