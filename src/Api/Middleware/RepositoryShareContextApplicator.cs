using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SaaSApp.MultiTenancy;
using SaaSApp.Repository.Application.Contracts;

namespace SaaSApp.Api.Middleware;

internal static class RepositoryShareTokenReader
{
    public static string? Read(HttpContext context) =>
        context.Request.Headers[RepositoryShareMiddleware.ShareTokenHeaderName].FirstOrDefault()
        ?? context.Request.Query["shareToken"].FirstOrDefault()
        ?? context.Request.Query["sharedtoken"].FirstOrDefault()
        ?? context.Request.Query["sharedToken"].FirstOrDefault();
}

/// <summary>Applies validated share context and source-tenant DB connection for repository item reads.</summary>
internal static class RepositoryShareContextApplicator
{
    public static async Task<IActionResult?> TryApplyAsync(
        HttpContext httpContext,
        IRepositoryItemShareService shareService,
        ITenantConnectionStringResolver connectionResolver,
        ITenantConnectionProvider connectionProvider,
        CancellationToken cancellationToken)
    {
        if (RepositoryShareContext.TryGet(httpContext, out _))
            return null;

        var shareToken = RepositoryShareTokenReader.Read(httpContext);
        if (string.IsNullOrWhiteSpace(shareToken))
            return null;

        if (!RepositorySharePathParser.TryParse(httpContext.Request.Path, out _, out _))
            return null;

        var email = httpContext.User.FindFirst("email")?.Value
            ?? httpContext.User.FindFirst(ClaimTypes.Email)?.Value
            ?? httpContext.User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress")?.Value;

        if (string.IsNullOrWhiteSpace(email))
        {
            return new UnauthorizedObjectResult(new { error = "Login required to view shared file." });
        }

        var access = await shareService.ResolveShareAccessAsync(
            shareToken.Trim(),
            email,
            cancellationToken: cancellationToken);

        if (access == null)
        {
            return new ObjectResult(new { error = "Invalid, expired, or unauthorized share link." })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }

        var sourceConnection = await connectionResolver.GetConnectionStringAsync(
            access.SourceTenantId,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(sourceConnection))
        {
            return new NotFoundObjectResult(new { error = "Source organization not found." });
        }

        connectionProvider.SetConnectionString(sourceConnection);
        httpContext.Items[RepositoryShareContext.HttpContextItemKey] = new RepositoryShareContext(access);
        return null;
    }
}
