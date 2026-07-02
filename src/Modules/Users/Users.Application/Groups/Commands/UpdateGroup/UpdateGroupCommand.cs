using MediatR;

namespace SaaSApp.Users.Application.Groups.Commands.UpdateGroup;

public record UpdateGroupCommand(
    Guid GroupId,
    string Name,
    string? Description,
    IReadOnlyList<Guid> UserIds) : IRequest<UpdateGroupCommandResult>;

public record UpdateGroupCommandResult(
    bool Success,
    string? Error = null,
    int StatusCode = 400);
