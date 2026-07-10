using System.Text.Json.Serialization;
using SaaSApp.Users.Application.Roles.Queries.ListPermissionCatalog;

namespace SaaSApp.Users.Application.Users;

/// <summary>Extended user profile matching POST/PUT JSON shape.</summary>
public sealed class UserExtendedResponse
{
    public Guid Id { get; set; }
    public string Email { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Role { get; set; } = "";
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? AuthStrategy { get; set; }
    public string? UserName { get; set; }

    [JsonPropertyName("LoginType")]
    public string? LoginType { get; set; }

    public int PasswordExpiryDays { get; set; }
    public DateTime? AccountExpiryDate { get; set; }
    public string? ForcePasswordResetOnLogin { get; set; }

    [JsonPropertyName("Job Title")]
    public string? JobTitle { get; set; }

    [JsonPropertyName("Employee Id")]
    public string? EmployeeId { get; set; }

    public string? Department { get; set; }

    [JsonPropertyName("Bussiness Unit")]
    public string? BusinessUnit { get; set; }

    [JsonPropertyName("Manager")]
    public string? Manager { get; set; }

    public string? Location { get; set; }
    public string[]? Group { get; set; }

    [JsonPropertyName("MFAuthentication")]
    public string? MfAuthentication { get; set; }

    [JsonPropertyName("MFA Methods")]
    public string? MfaMethods { get; set; }

    public string? PhoneNo { get; set; }
    public string? Language { get; set; }
    public string? CountryCode { get; set; }
    public string? AvatarPath { get; set; }
    public string? UiPreference { get; set; }
    public string? SecondaryEmail { get; set; }
    public string? UserType { get; set; }
    public string? IdCardPath { get; set; }
    public string? SignaturePath { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime? ModifiedAtUtc { get; set; }
    public Guid? ModifiedBy { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? PermissionCount { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<PermissionCategoryRow>? PermissionKeys { get; set; }
}
