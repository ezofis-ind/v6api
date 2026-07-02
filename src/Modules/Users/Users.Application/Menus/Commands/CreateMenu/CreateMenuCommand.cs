using MediatR;

namespace SaaSApp.Users.Application.Menus.Commands.CreateMenu;

public record CreateMenuCommand(
    string Key,
    string Label,
    string RoutePath,
    int SortOrder) : IRequest<CreateMenuCommandResult>;

public record CreateMenuCommandResult(
    bool Success,
    Guid? MenuId = null,
    string? Key = null,
    string? Label = null,
    string? Error = null,
    int StatusCode = 400);
