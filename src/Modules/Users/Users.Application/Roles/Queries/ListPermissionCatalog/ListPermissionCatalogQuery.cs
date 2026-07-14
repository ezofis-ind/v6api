using MediatR;
using SaaSApp.Users.Application.Contracts;

namespace SaaSApp.Users.Application.Roles.Queries.ListPermissionCatalog;

public record ListPermissionCatalogQuery : IRequest<ListPermissionCatalogQueryResult>;

public record PermissionCategoryCatalogItem(string Key, string Name);

public record ListPermissionCatalogQueryResult(IReadOnlyList<PermissionCategoryCatalogItem> Categories);

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
            .Select(category => new PermissionCategoryCatalogItem(category.Key, category.Name))
            .ToList();

        return new ListPermissionCatalogQueryResult(rows);
    }
}
