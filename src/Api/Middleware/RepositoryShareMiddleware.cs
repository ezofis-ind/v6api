using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SaaSApp.MultiTenancy;
using SaaSApp.Repository.Application.Contracts;

namespace SaaSApp.Api.Middleware;

/// <summary>
/// When <c>shareToken</c> / <c>sharedtoken</c> / <c>X-Share-Token</c> is sent on repository GET routes,
/// validates the share, switches to the source tenant DB, and blocks writes (read-only).
/// </summary>
public sealed class RepositoryShareMiddleware
{
    public const string ShareTokenHeaderName = "X-Share-Token";

    private readonly RequestDelegate _next;

    public RepositoryShareMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(
        HttpContext context,
        IRepositoryItemShareService shareService,
        ITenantConnectionStringResolver connectionResolver,
        ITenantConnectionProvider connectionProvider)
    {
        if (string.IsNullOrWhiteSpace(RepositoryShareTokenReader.Read(context))
            || !RepositorySharePathParser.TryParse(context.Request.Path, out _, out _))
        {
            await _next(context);
            return;
        }

        var isWrite = !HttpMethods.IsGet(context.Request.Method)
                      && !HttpMethods.IsHead(context.Request.Method)
                      && !HttpMethods.IsOptions(context.Request.Method);

        if (isWrite)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Shared file access is read-only. Use your own repository to upload new files."
            });
            return;
        }

        var errorResult = await RepositoryShareContextApplicator.TryApplyAsync(
            context, shareService, connectionResolver, connectionProvider, context.RequestAborted);

        if (errorResult != null)
        {
            await errorResult.ExecuteResultAsync(new ActionContext { HttpContext = context });
            return;
        }

        await _next(context);
    }
}

internal static class RepositorySharePathParser
{
    /// <summary>Matches .../repositories/{id} and .../repositories/{id}/items/{itemId}/...</summary>
    public static bool TryParse(PathString path, out Guid? repositoryId, out Guid? itemId)
    {
        repositoryId = null;
        itemId = null;

        var segments = path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();

        for (var i = 0; i < segments.Length; i++)
        {
            if (!string.Equals(segments[i], "repositories", StringComparison.OrdinalIgnoreCase))
                continue;

            if (i + 1 >= segments.Length
                || string.Equals(segments[i + 1], "share", StringComparison.OrdinalIgnoreCase)
                || !Guid.TryParse(segments[i + 1], out var repoId))
            {
                return false;
            }

            repositoryId = repoId;

            if (i + 3 < segments.Length
                && string.Equals(segments[i + 2], "items", StringComparison.OrdinalIgnoreCase)
                && Guid.TryParse(segments[i + 3], out var parsedItemId))
            {
                itemId = parsedItemId;
            }

            return true;
        }

        return false;
    }
}
