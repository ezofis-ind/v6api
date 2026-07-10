using SaaSApp.Catalog;
using SaaSApp.Users.Application.Contracts;

namespace SaaSApp.Api.Services;

public sealed class UserTenantRoleSync : IUserTenantRoleSync
{
    private readonly IUserTenantRegistry _userTenantRegistry;

    public UserTenantRoleSync(IUserTenantRegistry userTenantRegistry)
    {
        _userTenantRegistry = userTenantRegistry;
    }

    public Task SyncRoleForUserAsync(string email, Guid tenantId, string role, CancellationToken cancellationToken = default) =>
        _userTenantRegistry.AddOrUpdateAsync(email, tenantId, role, cancellationToken: cancellationToken);
}
