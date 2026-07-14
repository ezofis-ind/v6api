using MediatR;
using SaaSApp.Users.Application.Contracts;
using SaaSApp.Users.Application.Roles;
using SaaSApp.Users.Domain.Entities;

namespace SaaSApp.Users.Application.Roles.Commands.UpdateRole;

public sealed class UpdateRoleCommandHandler : IRequestHandler<UpdateRoleCommand, UpdateRoleCommandResult>
{
    private readonly IRoleRepository _roleRepository;
    private readonly IUserRepository _userRepository;
    private readonly IPermissionCategoryRepository _categoryRepository;
    private readonly IUserTenantRoleSync _userTenantRoleSync;
    private readonly ITenantContext _tenantContext;

    public UpdateRoleCommandHandler(
        IRoleRepository roleRepository,
        IUserRepository userRepository,
        IPermissionCategoryRepository categoryRepository,
        IUserTenantRoleSync userTenantRoleSync,
        ITenantContext tenantContext)
    {
        _roleRepository = roleRepository;
        _userRepository = userRepository;
        _categoryRepository = categoryRepository;
        _userTenantRoleSync = userTenantRoleSync;
        _tenantContext = tenantContext;
    }

    public async Task<UpdateRoleCommandResult> Handle(UpdateRoleCommand request, CancellationToken cancellationToken)
    {
        var hasName = !string.IsNullOrWhiteSpace(request.Name);
        var hasDescription = request.Description != null;
        var hasUsers = request.UserIds != null;
        var hasPermissions = request.PermissionKeys != null;

        if (!hasName && !hasDescription && !hasUsers && !hasPermissions)
            return Fail("No fields to update.");

        var role = await _roleRepository.GetByIdAsync(request.RoleId, cancellationToken);
        if (role == null)
            return Fail("Role not found.", 404);

        if (hasName)
        {
            var roleName = request.Name!.Trim();
            if (Role.IsReservedName(roleName))
                return Fail($"Role name '{roleName}' is reserved.");

            if (await _roleRepository.ExistsByNameAsync(roleName, request.RoleId, cancellationToken))
                return Fail($"A role named '{roleName}' already exists.");

            var oldName = role.Name;
            if (!string.Equals(oldName, roleName, StringComparison.OrdinalIgnoreCase))
            {
                role.UpdateName(roleName);

                var tenantId = _tenantContext.TenantId
                    ?? throw new InvalidOperationException("TenantId is required to rename a role.");

                var affectedEmails = await _userRepository.RenameRoleForUsersAsync(oldName, roleName, cancellationToken);
                foreach (var email in affectedEmails)
                    await _userTenantRoleSync.SyncRoleForUserAsync(email, tenantId, roleName, cancellationToken);
            }
        }

        if (hasDescription)
            role.UpdateDescription(request.Description);

        if (hasUsers)
        {
            var userIds = request.UserIds!.Distinct().ToList();
            if (userIds.Count > 0)
            {
                var existingUserCount = await _userRepository.CountExistingByIdsAsync(userIds, cancellationToken);
                if (existingUserCount != userIds.Count)
                    return Fail("One or more users were not found in this tenant.", 404);
            }

            role.ReplaceUsers(userIds);
        }

        if (hasPermissions)
        {
            if (request.PermissionKeys!.Count == 0)
                return Fail("At least one permission is required.");

            var (categoryKeys, permissionError) = await PermissionCategoryResolver.ResolveAsync(
                request.PermissionKeys,
                _categoryRepository,
                cancellationToken);
            if (permissionError != null)
                return Fail(permissionError);
            if (categoryKeys.Count == 0)
                return Fail("At least one permission is required.");

            role.ReplacePermissions(categoryKeys);
        }

        return new UpdateRoleCommandResult(Success: true, StatusCode: 204);
    }

    private static UpdateRoleCommandResult Fail(string error, int statusCode = 400) =>
        new(Success: false, Error: error, StatusCode: statusCode);
}
