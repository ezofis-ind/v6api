namespace SaaSApp.Workflow.Infrastructure.Options;

public sealed class ApAgentOptions
{
    public const string SectionName = "ApAgent";

    public bool Enabled { get; set; } = true;

    /// <summary>Python AP Agent service URL (POST { "startPayload": { ... } }). Move-next runs inside Python.</summary>
    public string PythonServiceUrl { get; set; } = string.Empty;

    public int TimeoutMinutes { get; set; } = 10;

    /// <summary>Public API base for progress callbacks, e.g. https://host/api/workflows</summary>
    public string? ApiBaseUrl { get; set; }
}
