using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace SaaSApp.Security;

public static class SecurityServiceCollectionExtensions
{
    public static IApplicationBuilder UseSecureHeaders(this IApplicationBuilder app)
    {
        return app.UseMiddleware<SecureHeadersMiddleware>();
    }
}
