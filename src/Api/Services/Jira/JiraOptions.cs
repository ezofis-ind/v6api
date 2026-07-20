namespace SaaSApp.Api.Services.Jira;

/// <summary>Jira Cloud settings for creating support tickets.</summary>
public sealed class JiraOptions
{
    public const string SectionName = "Jira";

    public bool Enabled { get; set; }

    /// <summary>Site root, e.g. https://ezofis.atlassian.net (not a UI list URL).</summary>
    public string BaseUrl { get; set; } = "https://ezofis.atlassian.net";

    public string Email { get; set; } = string.Empty;

    public string ApiToken { get; set; } = string.Empty;

    public string ProjectKey { get; set; } = "SUP";

    public string IssueType { get; set; } = "Task";
}
