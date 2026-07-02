using MediatR;

namespace SaaSApp.Users.Application.Groups.Commands.DeleteGroup;

public record DeleteGroupCommand(Guid GroupId) : IRequest<DeleteGroupCommandResult>;

public record DeleteGroupCommandResult(bool Found);
