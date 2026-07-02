using MediatR;
using SaaSApp.Users.Application.Contracts;

namespace SaaSApp.Users.Application.Roles.Queries.ListRoles;

public record ListRolesQuery : IRequest<ListRolesQueryResult>;

public record ListRolesQueryResult(IReadOnlyList<RoleListItem> Roles);

public sealed class ListRolesQueryHandler : IRequestHandler<ListRolesQuery, ListRolesQueryResult>
{
    private readonly IRoleRepository _roleRepository;

    public ListRolesQueryHandler(IRoleRepository roleRepository)
    {
        _roleRepository = roleRepository;
    }

    public async Task<ListRolesQueryResult> Handle(ListRolesQuery request, CancellationToken cancellationToken)
    {
        var roles = await _roleRepository.ListAsync(cancellationToken);
        return new ListRolesQueryResult(roles);
    }
}
