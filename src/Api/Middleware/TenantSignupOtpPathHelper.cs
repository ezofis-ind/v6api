using Microsoft.AspNetCore.Http;

namespace SaaSApp.Api.Middleware;

/// <summary>
/// Pre-signup OTP flows: tenant id does not exist yet; handlers use catalog DB only.
/// </summary>
internal static class TenantSignupOtpPathHelper
{
    internal static bool Matches(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        return path.Equals("/api/tenant/checkAuthenticate", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/api/tenant/validateOTP", StringComparison.OrdinalIgnoreCase);
    }
}
