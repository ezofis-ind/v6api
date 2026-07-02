using MediatR;
using SaaSApp.Users.Application.Contracts;
using SaaSApp.Users.Domain;

namespace SaaSApp.Users.Application.Roles.Queries.ListPermissionCatalog;

public record ListPermissionCatalogQuery : IRequest<ListPermissionCatalogQueryResult>;

public record PermissionActionItem(string Key, string Label);

public record PermissionMatrixItem(string Key, string ActionKey, string ActionLabel);

public record PermissionCategoryRow(string Key, string Name, IReadOnlyList<PermissionMatrixItem> Permissions);

public record ListPermissionCatalogQueryResult(
    IReadOnlyList<PermissionActionItem> Actions,
    IReadOnlyList<PermissionCategoryRow> Categories);

public sealed class ListPermissionCatalogQueryHandler : IRequestHandler<ListPermissionCatalogQuery, ListPermissionCatalogQueryResult>
{
    private readonly IPermissionCategoryRepository _categoryRepository;

    public ListPermissionCatalogQueryHandler(IPermissionCategoryRepository categoryRepository)
    {
        _categoryRepository = categoryRepository;
    }

    public async Task<ListPermissionCatalogQueryResult> Handle(ListPermissionCatalogQuery request, CancellationToken cancellationToken)
    {
        var actions = PermissionActions.AllActions
            .Select(a => new PermissionActionItem(a.Key, a.Label))
            .ToList();

        var categories = await _categoryRepository.ListActiveAsync(cancellationToken);
        var rows = categories.Select(category => new PermissionCategoryRow(
            category.Key,
            category.Name,
            actions.Select(action => new PermissionMatrixItem(
                PermissionKeyHelper.Build(category.Key, action.Key),
                action.Key,
                action.Label)).ToList())).ToList();

        return new ListPermissionCatalogQueryResult(actions, rows);
    }
}
