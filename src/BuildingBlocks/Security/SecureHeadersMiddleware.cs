using Microsoft.AspNetCore.Http;

namespace SaaSApp.Security;

/// <summary>
/// Adds security-related HTTP headers (OWASP recommendations, Azure API Management friendly).
/// </summary>
public sealed class SecureHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecureHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;

            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["X-XSS-Protection"] = "1; mode=block";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";

            return Task.CompletedTask;
        });

        await _next(context);
    }
}
