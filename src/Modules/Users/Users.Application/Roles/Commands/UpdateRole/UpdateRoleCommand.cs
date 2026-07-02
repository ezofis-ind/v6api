using MediatR;

namespace SaaSApp.Users.Application.Roles.Commands.UpdateRole;

public record UpdateRoleCommand(
    Guid RoleId,
    string? Name,
    string? Description,
    IReadOnlyList<Guid>? UserIds,
    IReadOnlyList<string>? PermissionKeys) : IRequest<UpdateRoleCommandResult>;

public record UpdateRoleCommandResult(
    bool Success,
    string? Error = null,
    int StatusCode = 400);
