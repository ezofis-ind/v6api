using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace SaaSApp.Logging;

public static class LoggingServiceCollectionExtensions
{
    public static WebApplicationBuilder AddSaaSAppLogging(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((context, services, configuration) =>
        {
            configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithEnvironmentName()
                .Enrich.WithMachineName()
                .Enrich.WithProperty("Application", context.HostingEnvironment.ApplicationName);

            if (context.HostingEnvironment.EnvironmentName == "Development")
                configuration.WriteTo.Console();
            else
                configuration.WriteTo.Console(new Serilog.Formatting.Json.JsonFormatter());
        });

        return builder;
    }

    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
    {
        return app.UseMiddleware<CorrelationIdMiddleware>();
    }
}
