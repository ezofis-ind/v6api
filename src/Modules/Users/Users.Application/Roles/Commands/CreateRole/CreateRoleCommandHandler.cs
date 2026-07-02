using MediatR;
using SaaSApp.Users.Application.Contracts;
using SaaSApp.Users.Domain;
using SaaSApp.Users.Domain.Entities;

namespace SaaSApp.Users.Application.Roles.Commands.CreateRole;

public sealed class CreateRoleCommandHandler : IRequestHandler<CreateRoleCommand, CreateRoleCommandResult>
{
    private readonly IRoleRepository _roleRepository;
    private readonly IUserRepository _userRepository;
    private readonly IPermissionValidator _permissionValidator;
    private readonly ITenantContext _tenantContext;

    public CreateRoleCommandHandler(
        IRoleRepository roleRepository,
        IUserRepository userRepository,
        IPermissionValidator permissionValidator,
        ITenantContext tenantContext)
    {
        _roleRepository = roleRepository;
        _userRepository = userRepository;
        _permissionValidator = permissionValidator;
        _tenantContext = tenantContext;
    }

    public async Task<CreateRoleCommandResult> Handle(CreateRoleCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("TenantId is required to create a role.");

        if (string.IsNullOrWhiteSpace(request.Name))
            return Fail("Role name is required.");

        var roleName = request.Name.Trim();
        if (Role.IsReservedName(roleName))
            return Fail($"Role name '{roleName}' is reserved.");

        if (request.UserIds == null || request.UserIds.Count == 0)
            return Fail("At least one user is required.");

        var userIds = request.UserIds.Distinct().ToList();
        var existingUserCount = await _userRepository.CountExistingByIdsAsync(userIds, cancellationToken);
        if (existingUserCount != userIds.Count)
            return Fail("One or more users were not found in this tenant.", 404);

        var permissionKeys = PermissionKeyHelper.NormalizeKeys(request.PermissionKeys ?? []);
        if (permissionKeys.Count == 0)
            return Fail("At least one permission is required.");

        var invalidPermission = await _permissionValidator.GetFirstInvalidKeyAsync(permissionKeys, cancellationToken);
        if (invalidPermission != null)
            return Fail($"Invalid permission key: '{invalidPermission}'.");

        if (await _roleRepository.ExistsByNameAsync(roleName, cancellationToken: cancellationToken))
            return Fail($"A role named '{roleName}' already exists.");

        var role = Role.Create(tenantId, roleName, request.Description);
        role.AssignUsers(userIds);
        role.AssignPermissions(permissionKeys);
        await _roleRepository.AddAsync(role, cancellationToken);

        return new CreateRoleCommandResult(
            Success: true,
            RoleId: role.Id,
            RoleName: role.Name,
            UserCount: userIds.Count,
            PermissionCount: permissionKeys.Count,
            StatusCode: 201);
    }

    private static CreateRoleCommandResult Fail(string error, int statusCode = 400) =>
        new(Success: false, Error: error, StatusCode: statusCode);
}
