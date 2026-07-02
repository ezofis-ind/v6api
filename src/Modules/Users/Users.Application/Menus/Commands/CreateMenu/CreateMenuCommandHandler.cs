using MediatR;
using SaaSApp.Users.Application.Contracts;
using SaaSApp.Users.Domain.Entities;

namespace SaaSApp.Users.Application.Menus.Commands.CreateMenu;

public sealed class CreateMenuCommandHandler : IRequestHandler<CreateMenuCommand, CreateMenuCommandResult>
{
    private readonly IMenuRepository _menuRepository;

    public CreateMenuCommandHandler(IMenuRepository menuRepository)
    {
        _menuRepository = menuRepository;
    }

    public async Task<CreateMenuCommandResult> Handle(CreateMenuCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Key))
            return Fail("Menu key is required.");

        if (string.IsNullOrWhiteSpace(request.Label))
            return Fail("Menu label is required.");

        if (string.IsNullOrWhiteSpace(request.RoutePath))
            return Fail("Menu route path is required.");

        var key = request.Key.Trim().ToLowerInvariant();
        var label = request.Label.Trim();
        var routePath = request.RoutePath.Trim();

        if (await _menuRepository.ExistsByKeyAsync(key, cancellationToken: cancellationToken))
            return Fail($"A menu with key '{key}' already exists.");

        var menu = Menu.Create(key, label, routePath, request.SortOrder);
        await _menuRepository.AddAsync(menu, cancellationToken);

        return new CreateMenuCommandResult(
            Success: true,
            MenuId: menu.Id,
            Key: menu.Key,
            Label: menu.Label,
            StatusCode: 201);
    }

    private static CreateMenuCommandResult Fail(string error, int statusCode = 400) =>
        new(Success: false, Error: error, StatusCode: statusCode);
}
