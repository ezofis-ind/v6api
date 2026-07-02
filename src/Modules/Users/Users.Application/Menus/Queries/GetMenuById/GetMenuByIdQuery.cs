using MediatR;
using SaaSApp.Users.Application.Contracts;

namespace SaaSApp.Users.Application.Menus.Queries.GetMenuById;

public record GetMenuByIdQuery(Guid MenuId) : IRequest<GetMenuByIdQueryResult?>;

public record GetMenuByIdQueryResult(
    Guid Id,
    string Key,
    string Label,
    string RoutePath,
    int SortOrder,
    bool IsSystem,
    DateTime CreatedAtUtc);

public sealed class GetMenuByIdQueryHandler : IRequestHandler<GetMenuByIdQuery, GetMenuByIdQueryResult?>
{
    private readonly IMenuRepository _menuRepository;

    public GetMenuByIdQueryHandler(IMenuRepository menuRepository)
    {
        _menuRepository = menuRepository;
    }

    public async Task<GetMenuByIdQueryResult?> Handle(GetMenuByIdQuery request, CancellationToken cancellationToken)
    {
        var menu = await _menuRepository.GetDetailByIdAsync(request.MenuId, cancellationToken);
        if (menu == null)
            return null;

        return new GetMenuByIdQueryResult(
            menu.Id,
            menu.Key,
            menu.Label,
            menu.RoutePath,
            menu.SortOrder,
            menu.IsSystem,
            menu.CreatedAtUtc);
    }
}
