using MediatR;
using SaaSApp.Users.Application.Contracts;

namespace SaaSApp.Users.Application.Menus.Commands.DeleteMenu;

public sealed class DeleteMenuCommandHandler : IRequestHandler<DeleteMenuCommand, DeleteMenuCommandResult>
{
    private readonly IMenuRepository _menuRepository;

    public DeleteMenuCommandHandler(IMenuRepository menuRepository)
    {
        _menuRepository = menuRepository;
    }

    public async Task<DeleteMenuCommandResult> Handle(DeleteMenuCommand request, CancellationToken cancellationToken)
    {
        var menu = await _menuRepository.GetByIdAsync(request.MenuId, cancellationToken);
        if (menu == null)
            return new DeleteMenuCommandResult(Found: false);

        if (menu.IsSystem)
            return new DeleteMenuCommandResult(Found: true, Error: "System menus cannot be deleted.", StatusCode: 400);

        menu.SoftDelete();
        return new DeleteMenuCommandResult(Found: true, StatusCode: 204);
    }
}
