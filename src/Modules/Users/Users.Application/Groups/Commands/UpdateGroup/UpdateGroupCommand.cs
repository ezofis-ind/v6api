using MediatR;

namespace SaaSApp.Users.Application.Groups.Commands.UpdateGroup;

public record UpdateGroupCommand(
    Guid GroupId,
    string? Name = null,
    string? Description = null,
    IReadOnlyList<Guid>? UserIds = null) : IRequest<UpdateGroupCommandResult>;

public record UpdateGroupCommandResult(
    bool Success,
    string? Error = null,
    int StatusCode = 400);
