using MediatR;
using SaaSApp.Users.Application.Contracts;

namespace SaaSApp.Users.Application.Menus.Queries.ListMenus;

public record ListMenusQuery : IRequest<ListMenusQueryResult>;

public record ListMenusQueryResult(IReadOnlyList<MenuListItem> Menus);

public sealed class ListMenusQueryHandler : IRequestHandler<ListMenusQuery, ListMenusQueryResult>
{
    private readonly IMenuRepository _menuRepository;

    public ListMenusQueryHandler(IMenuRepository menuRepository)
    {
        _menuRepository = menuRepository;
    }

    public async Task<ListMenusQueryResult> Handle(ListMenusQuery request, CancellationToken cancellationToken)
    {
        var menus = await _menuRepository.ListAsync(cancellationToken);
        return new ListMenusQueryResult(menus);
    }
}
