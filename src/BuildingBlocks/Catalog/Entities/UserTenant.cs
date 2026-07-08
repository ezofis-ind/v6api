namespace SaaSApp.Catalog.Entities;

/// <summary>
/// Links a user (by email) to a tenant and role. Used for "my organizations" at login.
/// Stored in the catalog database.
/// </summary>
public sealed class UserTenant
{
    public Guid Id { get; set; }
    public string Email { get; set; } = null!;
    public Guid TenantId { get; set; }
    public string Role { get; set; } = null!; // e.g. "Admin", "TenantUser"
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>Tenant <c>users.Users.Id</c>.</summary>
    public Guid? UserId { get; set; }

    /// <summary>Onboarding / pre-signup questions and answers as JSON.</summary>
    public string? PreQuestionsJson { get; set; }
}
