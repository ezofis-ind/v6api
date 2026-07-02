using MediatR;

namespace SaaSApp.Users.Application.Menus.Commands.UpdateMenu;

public record UpdateMenuCommand(
    Guid MenuId,
    string Label,
    string RoutePath,
    int SortOrder) : IRequest<UpdateMenuCommandResult>;

public record UpdateMenuCommandResult(
    bool Success,
    string? Error = null,
    int StatusCode = 400);
