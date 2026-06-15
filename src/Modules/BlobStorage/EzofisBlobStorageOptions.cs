namespace SaaSApp.BlobStorage;

/// <summary>
/// Shared Azure Blob settings for workflow JSON and repository files (EZOFIS provider).
/// Container per tenant: {ContainerPrefix}{tenantId:N} e.g. ezts51966bf0...
/// </summary>
public sealed class EzofisBlobStorageOptions
{
    public const string SectionName = "EzofisBlobStorage";

    public string? ConnectionString { get; set; }

    /// <summary>Prefix for tenant containers (default ezts).</summary>
    public string ContainerPrefix { get; set; } = "ezts";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ConnectionString);

    public static string BuildContainerName(string containerPrefix, Guid tenantId)
    {
        var prefix = string.IsNullOrWhiteSpace(containerPrefix) ? "ezts" : containerPrefix.Trim().ToLowerInvariant();
        return $"{prefix}{tenantId:N}".ToLowerInvariant();
    }
}
