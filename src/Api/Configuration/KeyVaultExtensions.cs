using Microsoft.Extensions.Configuration;

namespace SaaSApp.Api.Configuration;

/// <summary>
/// Azure Key Vault configuration. In production, use AddAzureKeyVault to replace placeholders.
/// Placeholders in appsettings: --KEYVAULT--Section__Key-- are replaced by Key Vault secrets.
/// </summary>
public static class KeyVaultExtensions
{
    public static IConfigurationBuilder AddKeyVaultIfConfigured(this IConfigurationBuilder config, IConfiguration existing)
    {
        var keyVaultEndpoint = existing["KeyVault:Endpoint"];
        if (string.IsNullOrEmpty(keyVaultEndpoint))
            return config;

        // Production: uncomment and configure for Azure Key Vault
        // config.AddAzureKeyVault(new Uri(keyVaultEndpoint), new DefaultAzureCredential());
        return config;
    }
}
