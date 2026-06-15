namespace SaaSApp.Api.Configuration;

/// <summary>
/// Service-to-service authentication readiness.
/// Use Managed Identity (DefaultAzureCredential) or client credentials to call downstream APIs.
/// Example: AddHttpClient with AddClientAccessTokenHandler (Microsoft.Identity.Web).
/// </summary>
public static class ServiceToServiceAuth
{
    // In Program.cs or extension:
    // builder.Services.AddHttpClient<IDownstreamApi, DownstreamApi>()
    //     .AddMicrosoftIdentityAppAuthenticationHandler("Downstream", builder.Configuration);
    // appsettings: "Downstream": { "BaseUrl": "...", "Scopes": "api://..." }
}
