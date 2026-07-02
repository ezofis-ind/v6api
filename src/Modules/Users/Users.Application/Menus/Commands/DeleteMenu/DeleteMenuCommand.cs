using MediatR;

namespace SaaSApp.Users.Application.Menus.Commands.DeleteMenu;

public record DeleteMenuCommand(Guid MenuId) : IRequest<DeleteMenuCommandResult>;

public record DeleteMenuCommandResult(bool Found, string? Error = null, int StatusCode = 404);
