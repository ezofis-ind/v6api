using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Options;
using SaaSApp.ActivityLog.Application.Contracts;
using SaaSApp.ActivityLog.Infrastructure.Options;
using SaaSApp.Logging;
using SaaSApp.MultiTenancy;

namespace SaaSApp.Api.Middleware;

/// <summary>
/// Records authenticated API access and 401 unauthenticated attempts into the tenant activity log.
/// Writes are deferred to Response.OnCompleted so StatusCode matches the client response.
/// </summary>
public sealed class ApiActivityLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ActivityLogOptions _options;

    public ApiActivityLoggingMiddleware(RequestDelegate next, IOptions<ActivityLogOptions> options)
    {
        _next = next;
        _options = options.Value;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ITenantProvider tenantProvider,
        ITenantConnectionProvider connectionProvider,
        IActivityLogWriter writer)
    {
        if (!_options.Enabled || !ShouldConsiderPath(context.Request.Path, context.Request.Method))
        {
            await _next(context);
            return;
        }

        var tenantId = tenantProvider.GetTenantId();
        var connectionString = connectionProvider.ConnectionString;
        var stopwatch = Stopwatch.StartNew();

        context.Response.OnCompleted(() =>
        {
            try
            {
                if (tenantId == null || string.IsNullOrEmpty(connectionString))
                    return Task.CompletedTask;

                var statusCode = ResolveStatusCode(context);
                var isAuthenticated = context.User.Identity?.IsAuthenticated == true;
                var shouldLog = isAuthenticated
                    || (_options.LogUnauthenticated401 && statusCode == StatusCodes.Status401Unauthorized);

                if (!shouldLog)
                    return Task.CompletedTask;

                var entry = BuildEntry(context, tenantId.Value, statusCode, stopwatch.ElapsedMilliseconds);
                writer.Enqueue(entry, connectionString);
            }
            catch
            {
                // Never break response completion.
            }

            return Task.CompletedTask;
        });

        await _next(context);
    }

    private bool ShouldConsiderPath(PathString path, string method)
    {
        var value = path.Value ?? string.Empty;
        if (!value.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
            return false;

        if (HttpMethods.IsOptions(method))
            return false;

        if (value.StartsWith("/health", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/hangfire", StringComparison.OrdinalIgnoreCase))
            return false;

        if (TenantSignupOtpPathHelper.MatchesPath(value))
            return false;

        foreach (var prefix in _options.ExcludedPathPrefixes)
        {
            if (!string.IsNullOrWhiteSpace(prefix)
                && value.StartsWith(prefix.Trim(), StringComparison.OrdinalIgnoreCase))
                return false;
        }

        foreach (var prefix in _options.PublicPathPrefixes)
        {
            if (!string.IsNullOrWhiteSpace(prefix)
                && value.StartsWith(prefix.Trim(), StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    private static int ResolveStatusCode(HttpContext context)
    {
        var statusCode = context.Response.StatusCode;
        if (statusCode >= 400)
            return statusCode;

        var errorFeature = context.Features.Get<IExceptionHandlerFeature>();
        if (errorFeature?.Error != null)
            return StatusCodes.Status500InternalServerError;

        return statusCode;
    }

    private ActivityLogEntry BuildEntry(HttpContext context, Guid tenantId, int statusCode, long durationMs)
    {
        var request = context.Request;
        var path = request.Path.Value ?? string.Empty;
        var queryString = request.QueryString.HasValue ? request.QueryString.Value : null;
        if (queryString != null && queryString.Length > _options.MaxQueryStringLength)
            queryString = queryString[.._options.MaxQueryStringLength];

        var userAgent = request.Headers.UserAgent.ToString();
        if (userAgent.Length > _options.MaxUserAgentLength)
            userAgent = userAgent[.._options.MaxUserAgentLength];

        context.Items.TryGetValue(CorrelationIdMiddleware.CorrelationIdItemKey, out var correlationObj);
        var correlationId = correlationObj as string;

        return new ActivityLogEntry(
            Guid.NewGuid(),
            tenantId,
            GetUserId(context.User),
            GetUserEmail(context.User),
            request.Method,
            path,
            queryString,
            statusCode,
            (int)Math.Min(durationMs, int.MaxValue),
            correlationId,
            GetClientIp(context),
            string.IsNullOrEmpty(userAgent) ? null : userAgent,
            DateTime.UtcNow);
    }

    private static Guid? GetUserId(ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? user.FindFirstValue("sub")
            ?? user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("userId")
            ?? user.FindFirstValue("oid");
        return Guid.TryParse(sub, out var id) ? id : null;
    }

    private static string? GetUserEmail(ClaimsPrincipal user)
    {
        return user.FindFirstValue("email")
            ?? user.FindFirstValue(ClaimTypes.Email)
            ?? user.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress");
    }

    private static string? GetClientIp(HttpContext context)
    {
        var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
            return forwarded.Split(',')[0].Trim();

        return context.Connection.RemoteIpAddress?.ToString();
    }
}
