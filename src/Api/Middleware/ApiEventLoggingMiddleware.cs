using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Options;
using SaaSApp.ActivityLog.Application;
using SaaSApp.ActivityLog.Application.Contracts;
using SaaSApp.ActivityLog.Infrastructure.Options;
using SaaSApp.Logging;
using SaaSApp.MultiTenancy;

namespace SaaSApp.Api.Middleware;

/// <summary>
/// Writes business Event Log rows for authenticated API completions (and Auth login routes).
/// Defers to Response.OnCompleted so StatusCode matches the client response (except auth login,
/// which buffers the response to read JWT actor claims before enqueue).
/// </summary>
public sealed class ApiEventLoggingMiddleware
{
    private const int MaxBodyPeekBytes = 8192;

    private readonly RequestDelegate _next;
    private readonly EventLogOptions _options;

    public ApiEventLoggingMiddleware(RequestDelegate next, IOptions<EventLogOptions> options)
    {
        _next = next;
        _options = options.Value;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ITenantProvider tenantProvider,
        ITenantConnectionProvider connectionProvider,
        IEventLogWriter writer)
    {
        if (!_options.Enabled || !ShouldConsiderPath(context.Request.Path, context.Request.Method))
        {
            await _next(context);
            return;
        }

        var tenantId = tenantProvider.GetTenantId();
        var connectionString = connectionProvider.ConnectionString;
        var method = context.Request.Method;
        var path = context.Request.Path.Value ?? string.Empty;

        var isAuthLogin = IsAuthLoginRoute(method, path);
        EventLogSubject subject = new();
        if (isAuthLogin || ShouldPeekSubjectBody(method, path))
            subject = await TryPeekSubjectFromBodyAsync(context);

        if (string.IsNullOrWhiteSpace(subject.FileName)
            && IsRepositoryUploadPath(method, path)
            && context.Request.HasFormContentType)
        {
            var uploadName = TryGetMultipartFileName(context);
            if (!string.IsNullOrWhiteSpace(uploadName))
                subject = subject with { FileName = uploadName };
        }

        if (isAuthLogin)
        {
            await InvokeAuthLoginAsync(
                context,
                tenantId,
                connectionString,
                method,
                path,
                subject,
                writer);
            return;
        }

        context.Response.OnCompleted(() =>
        {
            try
            {
                if (tenantId == null || string.IsNullOrEmpty(connectionString))
                    return Task.CompletedTask;

                var statusCode = ResolveStatusCode(context);
                var isAuthenticated = context.User.Identity?.IsAuthenticated == true;
                if (!isAuthenticated)
                    return Task.CompletedTask;

                EnqueueEntry(
                    context,
                    tenantId.Value,
                    connectionString,
                    method,
                    path,
                    statusCode,
                    subject,
                    loginEmailFromJwt: null,
                    loginDisplayNameFromJwt: null,
                    loginUserIdFromJwt: null,
                    isAuthLogin: false,
                    writer);
            }
            catch
            {
                // Never break response completion.
            }

            return Task.CompletedTask;
        });

        await _next(context);
    }

    private async Task InvokeAuthLoginAsync(
        HttpContext context,
        Guid? tenantId,
        string? connectionString,
        string method,
        string path,
        EventLogSubject subject,
        IEventLogWriter writer)
    {
        var originalBody = context.Response.Body;
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;
        try
        {
            await _next(context);

            string? loginEmailFromJwt = null;
            string? loginDisplayNameFromJwt = null;
            Guid? loginUserIdFromJwt = null;

            buffer.Position = 0;
            var responseBytes = buffer.ToArray();
            if (IsSuccessStatus(context.Response.StatusCode) && responseBytes.Length > 0)
            {
                var claims = TryReadActorFromLoginResponse(responseBytes);
                loginEmailFromJwt = claims.Email;
                loginDisplayNameFromJwt = claims.DisplayName;
                loginUserIdFromJwt = claims.UserId;
            }

            try
            {
                if (tenantId != null
                    && !string.IsNullOrEmpty(connectionString)
                    && _options.LogAuthLoginUnauthenticated)
                {
                    EnqueueEntry(
                        context,
                        tenantId.Value,
                        connectionString,
                        method,
                        path,
                        ResolveStatusCode(context),
                        subject,
                        loginEmailFromJwt,
                        loginDisplayNameFromJwt,
                        loginUserIdFromJwt,
                        isAuthLogin: true,
                        writer);
                }
            }
            catch
            {
                // Never break the login response.
            }

            buffer.Position = 0;
            await buffer.CopyToAsync(originalBody);
        }
        finally
        {
            context.Response.Body = originalBody;
        }
    }

    private static void EnqueueEntry(
        HttpContext context,
        Guid tenantId,
        string connectionString,
        string method,
        string path,
        int statusCode,
        EventLogSubject subject,
        string? loginEmailFromJwt,
        string? loginDisplayNameFromJwt,
        Guid? loginUserIdFromJwt,
        bool isAuthLogin,
        IEventLogWriter writer)
    {
        var mapped = EventLogRouteMapper.Map(method, path, statusCode, subject);
        context.Items.TryGetValue(CorrelationIdMiddleware.CorrelationIdItemKey, out var correlationObj);

        var (userId, userDisplayName, userEmail) = ResolveActor(
            context.User,
            isAuthLogin,
            subject,
            loginEmailFromJwt,
            loginDisplayNameFromJwt,
            loginUserIdFromJwt);

        var entry = new EventLogEntry(
            Guid.NewGuid(),
            tenantId,
            userId,
            userDisplayName,
            userEmail,
            Truncate(mapped.EventTitle, 512)!,
            Truncate(mapped.EventType, 128)!,
            Truncate(mapped.Category, 64)!,
            Truncate(mapped.Severity, 32)!,
            GetClientIp(context),
            method,
            Truncate(path, 512),
            statusCode,
            correlationObj as string,
            DateTime.UtcNow);

        writer.Enqueue(entry, connectionString);
    }

    private static (Guid? UserId, string? DisplayName, string? Email) ResolveActor(
        ClaimsPrincipal user,
        bool isAuthLogin,
        EventLogSubject subject,
        string? loginEmailFromJwt,
        string? loginDisplayNameFromJwt,
        Guid? loginUserIdFromJwt)
    {
        var email = FirstNonEmpty(
            GetUserEmail(user),
            isAuthLogin ? loginEmailFromJwt : null,
            isAuthLogin ? subject.Email : null);

        var displayName = FirstNonEmpty(
            GetUserDisplayName(user),
            isAuthLogin ? loginDisplayNameFromJwt : null,
            isAuthLogin ? subject.DisplayName : null,
            isAuthLogin ? email : null);

        var userId = GetUserId(user) ?? (isAuthLogin ? loginUserIdFromJwt : null);

        return (userId, displayName, email);
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return null;
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

    private static bool IsAuthLoginRoute(string method, string path)
    {
        if (!HttpMethods.IsPost(method))
            return false;

        var normalized = path.TrimEnd('/');
        return normalized.Equals("/api/auth/ezofis/login", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("/api/auth/social/login", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("/api/auth/2fa/complete", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldPeekSubjectBody(string method, string path)
    {
        if (!HttpMethods.IsPost(method)
            && !HttpMethods.IsPut(method)
            && !HttpMethods.IsPatch(method))
            return false;

        var normalized = path.TrimEnd('/');
        return normalized.StartsWith("/api/users", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("/api/workflows", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("/api/workflow", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("/api/form", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("/api/repositories", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRepositoryUploadPath(string method, string path)
    {
        if (!HttpMethods.IsPost(method))
            return false;

        var normalized = path.TrimEnd('/');
        return normalized.StartsWith("/api/repositories/", StringComparison.OrdinalIgnoreCase)
            && (normalized.EndsWith("/items/upload", StringComparison.OrdinalIgnoreCase)
                || normalized.EndsWith("/items/upload-archive", StringComparison.OrdinalIgnoreCase));
    }

    private static string? TryGetMultipartFileName(HttpContext context)
    {
        try
        {
            var file = context.Request.Form.Files.FirstOrDefault();
            var name = file?.FileName;
            return string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        }
        catch
        {
            return null;
        }
    }

    private static async Task<EventLogSubject> TryPeekSubjectFromBodyAsync(HttpContext context)
    {
        try
        {
            if (context.Request.HasFormContentType)
                return new EventLogSubject();

            if (context.Request.ContentLength is 0)
                return new EventLogSubject();

            context.Request.EnableBuffering();
            context.Request.Body.Position = 0;

            var toRead = (int)Math.Min(
                MaxBodyPeekBytes,
                context.Request.ContentLength ?? MaxBodyPeekBytes);
            var buffer = new byte[toRead];
            var read = await context.Request.Body.ReadAsync(buffer.AsMemory(0, buffer.Length));
            context.Request.Body.Position = 0;

            if (read == 0)
                return new EventLogSubject();

            return EventLogSubjectParser.Parse(buffer.AsSpan(0, read));
        }
        catch
        {
            try { context.Request.Body.Position = 0; } catch { /* ignore */ }
            return new EventLogSubject();
        }
    }

    private static (string? Email, string? DisplayName, Guid? UserId) TryReadActorFromLoginResponse(byte[] responseBytes)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBytes);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return (null, null, null);

            var accessToken = GetJsonStringIgnoreCase(root, "accessToken");
            if (string.IsNullOrWhiteSpace(accessToken))
                return (null, null, null);

            var handler = new JwtSecurityTokenHandler();
            if (!handler.CanReadToken(accessToken))
                return (null, null, null);

            var token = handler.ReadJwtToken(accessToken);
            var email = FirstNonEmpty(
                token.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Email)?.Value,
                token.Claims.FirstOrDefault(c => c.Type == "email")?.Value,
                token.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value);

            var displayName = FirstNonEmpty(
                token.Claims.FirstOrDefault(c => c.Type == "name")?.Value,
                token.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value,
                token.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.UniqueName)?.Value,
                email);

            var sub = FirstNonEmpty(
                token.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value,
                token.Claims.FirstOrDefault(c => c.Type == "sub")?.Value,
                token.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value);

            Guid? userId = Guid.TryParse(sub, out var id) ? id : null;
            return (email, displayName, userId);
        }
        catch
        {
            return (null, null, null);
        }
    }

    private static string? GetJsonStringIgnoreCase(JsonElement root, string propertyName)
    {
        foreach (var prop in root.EnumerateObject())
        {
            if (!prop.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
                continue;
            if (prop.Value.ValueKind != JsonValueKind.String)
                return null;
            var value = prop.Value.GetString();
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        return null;
    }

    private static bool IsSuccessStatus(int statusCode) => statusCode is >= 200 and < 300;

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

    private static string? GetUserDisplayName(ClaimsPrincipal user)
    {
        return user.FindFirstValue("name")
            ?? user.FindFirstValue(ClaimTypes.Name)
            ?? user.FindFirstValue("display_name")
            ?? user.FindFirstValue("preferred_username")
            ?? GetUserEmail(user);
    }

    private static string? GetClientIp(HttpContext context)
    {
        var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
            return forwarded.Split(',')[0].Trim();

        return context.Connection.RemoteIpAddress?.ToString();
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
            return value;
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
