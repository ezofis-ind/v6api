using MediatR;

namespace SaaSApp.Users.Application.Groups.Commands.CreateGroup;

public record CreateGroupCommand(
    string Name,
    string? Description,
    IReadOnlyList<Guid> UserIds) : IRequest<CreateGroupCommandResult>;

public record CreateGroupCommandResult(
    bool Success,
    Guid? GroupId = null,
    string? GroupName = null,
    int UserCount = 0,
    string? Error = null,
    int StatusCode = 400);
