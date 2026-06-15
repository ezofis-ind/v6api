using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Http;
using SaaSApp.Workflow.Application.Contracts;

namespace SaaSApp.Workflow.Infrastructure;

public sealed class CurrentUserProvider : ICurrentUserProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? GetUserId()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
            return null;

        var subClaim = user.FindFirst(JwtRegisteredClaimNames.Sub)
            ?? user.FindFirst("sub")
            ?? user.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");

        if (subClaim == null || !Guid.TryParse(subClaim.Value, out var userId))
            return null;

        return userId;
    }
}
