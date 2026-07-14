using MediatR;
using SaaSApp.Users.Application.Contracts;

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
        var categories = await _categoryRepository.ListActiveAsync(cancellationToken);
        var rows = categories
            .Select(category => new PermissionCategoryRow(
                category.Key,
                category.Name,
                Array.Empty<PermissionMatrixItem>()))
            .ToList();

        // Actions / matrix cells are unused for category-only role permissions; kept empty for API shape stability.
        return new ListPermissionCatalogQueryResult(Array.Empty<PermissionActionItem>(), rows);
    }
}
