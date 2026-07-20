using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SaaSApp.Workflow.Infrastructure.Services;

internal sealed class ConnectorOAuthStatePayload
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid ConnectorId { get; set; }
    public string ProviderCode { get; set; } = string.Empty;
    public string? SuccessRedirectUrl { get; set; }
    public long Exp { get; set; }
}

internal static class ConnectorOAuthStateHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string Create(ConnectorOAuthStatePayload payload, string signingKey)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var body = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        var sig = Sign(body, signingKey);
        return $"{body}.{sig}";
    }

    public static ConnectorOAuthStatePayload Parse(string state, string signingKey)
    {
        if (string.IsNullOrWhiteSpace(state))
            throw new InvalidOperationException("OAuth state is missing.");

        var parts = state.Split('.', 2);
        if (parts.Length != 2)
            throw new InvalidOperationException("OAuth state is invalid.");

        var expected = Sign(parts[0], signingKey);
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expected),
                Encoding.UTF8.GetBytes(parts[1])))
            throw new InvalidOperationException("OAuth state signature is invalid.");

        var json = Encoding.UTF8.GetString(Convert.FromBase64String(parts[0]));
        var payload = JsonSerializer.Deserialize<ConnectorOAuthStatePayload>(json, JsonOptions)
            ?? throw new InvalidOperationException("OAuth state payload is invalid.");

        if (payload.Exp < DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            throw new InvalidOperationException("OAuth state has expired. Start authorization again.");

        return payload;
    }

    private static string Sign(string body, string signingKey)
    {
        if (string.IsNullOrWhiteSpace(signingKey))
            throw new InvalidOperationException("ConnectorOAuth state signing key is not configured.");

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(signingKey));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
        return Convert.ToBase64String(hash);
    }
}
