namespace SaaSApp.Catalog.Entities;

/// <summary>Global OAuth app configuration for a connector provider (Catalog DB).</summary>
public sealed class ConnectorProvider
{
    public Guid Id { get; set; }
    public string ProviderCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string AuthUrl { get; set; } = string.Empty;
    public string TokenUrl { get; set; } = string.Empty;
    public string Scopes { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string? ExtraConfigJson { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ModifiedAtUtc { get; set; }
}
