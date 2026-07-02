using MediatR;
using SaaSApp.Users.Application.Contracts;

namespace SaaSApp.Users.Application.Menus.Commands.UpdateMenu;

public sealed class UpdateMenuCommandHandler : IRequestHandler<UpdateMenuCommand, UpdateMenuCommandResult>
{
    private readonly IMenuRepository _menuRepository;

    public UpdateMenuCommandHandler(IMenuRepository menuRepository)
    {
        _menuRepository = menuRepository;
    }

    public async Task<UpdateMenuCommandResult> Handle(UpdateMenuCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Label))
            return Fail("Menu label is required.");

        if (string.IsNullOrWhiteSpace(request.RoutePath))
            return Fail("Menu route path is required.");

        var menu = await _menuRepository.GetByIdAsync(request.MenuId, cancellationToken);
        if (menu == null)
            return Fail("Menu not found.", 404);

        menu.Update(request.Label, request.RoutePath, request.SortOrder);
        return new UpdateMenuCommandResult(Success: true, StatusCode: 204);
    }

    private static UpdateMenuCommandResult Fail(string error, int statusCode = 400) =>
        new(Success: false, Error: error, StatusCode: statusCode);
}
