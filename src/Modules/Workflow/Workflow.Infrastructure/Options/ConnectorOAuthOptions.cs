namespace SaaSApp.Workflow.Infrastructure.Options;

public sealed class ConnectorOAuthOptions
{
    public const string SectionName = "ConnectorOAuth";

    /// <summary>HMAC key for OAuth state. Falls back to EzofisAuth:SigningKey when empty.</summary>
    public string? StateSigningKey { get; set; }

    /// <summary>Minutes until authorize state expires.</summary>
    public int StateTtlMinutes { get; set; } = 15;

    /// <summary>Default UI redirect after successful OAuth callback.</summary>
    public string? DefaultSuccessRedirectUrl { get; set; }

    /// <summary>Refresh access token this many minutes before expiry.</summary>
    public int RefreshSkewMinutes { get; set; } = 5;
}
