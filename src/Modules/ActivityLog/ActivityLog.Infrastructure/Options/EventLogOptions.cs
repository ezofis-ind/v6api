namespace SaaSApp.ActivityLog.Infrastructure.Options;

public sealed class EventLogOptions
{
    public const string SectionName = "EventLog";

    public bool Enabled { get; set; } = true;

    /// <summary>Also log Auth login routes when unauthenticated (tenant header available).</summary>
    public bool LogAuthLoginUnauthenticated { get; set; } = true;

    public string[] ExcludedPathPrefixes { get; set; } =
    [
        "/api/event-logs",
        "/api/activity-logs"
    ];

    public string[] PublicPathPrefixes { get; set; } =
    [
        "/api/signup",
        "/api/Signup",
        "/api/tenant/checkAuthenticate",
        "/api/tenant/validateOTP"
    ];
}
