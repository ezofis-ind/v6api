using MediatR;
using SaaSApp.Users.Application.Contracts;

namespace SaaSApp.Users.Application.Roles.Queries.GetRoleById;

public record GetRoleByIdQuery(Guid RoleId) : IRequest<GetRoleByIdQueryResult?>;

public record GetRoleByIdQueryResult(
    Guid Id,
    string RoleName,
    string? Description,
    DateTime CreatedAtUtc,
    IReadOnlyList<Guid> Users,
    IReadOnlyList<string> Permissions);

public sealed class GetRoleByIdQueryHandler : IRequestHandler<GetRoleByIdQuery, GetRoleByIdQueryResult?>
{
    private readonly IRoleRepository _roleRepository;

    public GetRoleByIdQueryHandler(IRoleRepository roleRepository)
    {
        _roleRepository = roleRepository;
    }

    public async Task<GetRoleByIdQueryResult?> Handle(GetRoleByIdQuery request, CancellationToken cancellationToken)
    {
        var role = await _roleRepository.GetDetailByIdAsync(request.RoleId, cancellationToken);
        if (role == null)
            return null;

        return new GetRoleByIdQueryResult(
            role.Id,
            role.Name,
            role.Description,
            role.CreatedAtUtc,
            role.UserIds,
            role.PermissionKeys);
    }
}
