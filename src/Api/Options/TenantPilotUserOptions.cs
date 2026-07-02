namespace SaaSApp.Api.Options;

/// <summary>
/// Default service user created for each tenant on signup.
/// Used by AP Agent (Python) to call Ezofis login and access tenant APIs.
/// </summary>
public sealed class TenantPilotUserOptions
{
    public const string SectionName = "TenantPilotUser";

    public bool Enabled { get; set; } = true;

    public string Email { get; set; } = "pilot@ezofis.com";

    public string Password { get; set; } = string.Empty;

    public string DisplayName { get; set; } = "AP Agent Pilot";

    public string Role { get; set; } = "TenantUser";
}
