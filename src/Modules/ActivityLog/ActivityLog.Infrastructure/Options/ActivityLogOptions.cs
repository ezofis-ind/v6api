namespace SaaSApp.ActivityLog.Infrastructure.Options;

public sealed class ActivityLogOptions
{
    public const string SectionName = "ActivityLog";

    public bool Enabled { get; set; } = false;

    public bool LogUnauthenticated401 { get; set; } = true;

    public string[] ExcludedPathPrefixes { get; set; } =
    [
        "/api/activity-logs"
    ];

    public string[] PublicPathPrefixes { get; set; } =
    [
        "/api/signup",
        "/api/Signup",
        "/api/tenant/checkAuthenticate",
        "/api/tenant/validateOTP"
    ];

    public int MaxQueryStringLength { get; set; } = 1024;

    public int MaxUserAgentLength { get; set; } = 512;
}
