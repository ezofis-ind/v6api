using SaaSApp.SharedKernel.Domain;
using SaaSApp.SharedKernel.Domain.Interfaces;
using SaaSApp.Users.Domain.Events;

namespace SaaSApp.Users.Domain.Entities;

/// <summary>User entity for a tenant. Supports Ezofis, Google, Office365 auth strategies and 2FA.</summary>
public sealed class User : Entity<Guid>, ITenantEntity
{
    public const string RoleAdmin = "Admin";
    public const string RoleTenantUser = "TenantUser";
    public const string AuthStrategyEzofis = "Ezofis";
    public const string AuthStrategyGoogle = "Google";
    public const string AuthStrategyOffice365 = "Office365";

    public Guid TenantId { get; private set; }
    public string Email { get; private set; } = null!;
    public string DisplayName { get; private set; } = null!;
    public string Role { get; private set; } = "TenantUser";
    public DateTime CreatedAtUtc { get; private set; }

    // Profile & identity
    public string? FirstName { get; private set; }
    public string? LastName { get; private set; }
    public string? ProfileId { get; private set; }
    public string? PhoneNo { get; private set; }
    public string? SecondaryEmail { get; private set; }
    public string? Language { get; private set; }
    public string? CountryCode { get; private set; }

    // Organization
    public string? Department { get; private set; }
    public string? JobTitle { get; private set; }
    public Guid? ManagerId { get; private set; }
    public string? UserType { get; private set; }

    // Auth
    public string? AuthStrategy { get; private set; }
    public string? LoginType { get; private set; }
    public string? LoginName { get; private set; }
    public string? PasswordHash { get; private set; }
    public string? PinHash { get; private set; }
    public string? DeviceId { get; private set; }
    public bool TwoFactorAuthentication { get; private set; }
    public string? TotpSecretEncrypted { get; private set; }
    public int? PasswordAge { get; private set; }
    public string? GoogleSubjectId { get; private set; }
    public string? MicrosoftOid { get; private set; }

    // Profile assets
    public string? AvatarPath { get; private set; }
    public string? IdCardPath { get; private set; }
    public string? SignaturePath { get; private set; }

    // Preferences
    public string? UiPreference { get; private set; }

    // Audit
    public DateTime? ModifiedAtUtc { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public Guid? ModifiedBy { get; private set; }
    public bool IsDeleted { get; private set; }

    private User() { } // EF

    private User(Guid id, Guid tenantId, string email, string displayName, string role,
        string? firstName, string? lastName, string? authStrategy)
    {
        Id = id;
        TenantId = tenantId;
        Email = email;
        DisplayName = displayName;
        Role = role ?? RoleTenantUser;
        FirstName = firstName?.Trim();
        LastName = lastName?.Trim();
        AuthStrategy = authStrategy;
        CreatedAtUtc = DateTime.UtcNow;
        RaiseDomainEvent(new UserCreatedEvent(Guid.NewGuid(), DateTime.UtcNow, id, tenantId, email, displayName));
    }

    /// <summary>Create a new user in the tenant.</summary>
    public static User Create(Guid tenantId, string email, string displayName, string? role = null,
        string? firstName = null, string? lastName = null, string? authStrategy = null)
    {
        return new User(Guid.NewGuid(), tenantId, email, displayName, role ?? RoleTenantUser, firstName, lastName, authStrategy ?? AuthStrategyEzofis);
    }

    /// <summary>Update profile fields. Only non-null parameters are applied.</summary>
    public void Update(string? displayName = null, string? role = null, string? firstName = null, string? lastName = null,
        string? phoneNo = null, string? department = null, string? jobTitle = null, string? language = null,
        string? countryCode = null, string? avatarPath = null, string? uiPreference = null, Guid? modifiedBy = null)
    {
        if (displayName != null) DisplayName = displayName.Trim();
        if (role != null) Role = role.Trim();
        if (firstName != null) FirstName = firstName.Trim();
        if (lastName != null) LastName = lastName.Trim();
        if (phoneNo != null) PhoneNo = phoneNo.Trim();
        if (department != null) Department = department.Trim();
        if (jobTitle != null) JobTitle = jobTitle.Trim();
        if (language != null) Language = language.Trim();
        if (countryCode != null) CountryCode = countryCode.Trim();
        if (avatarPath != null) AvatarPath = avatarPath.Trim();
        if (uiPreference != null) UiPreference = uiPreference.Trim();
        ModifiedBy = modifiedBy;
        ModifiedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Enable 2FA by storing encrypted TOTP secret.</summary>
    public void EnableTwoFactor(string encryptedTotpSecret)
    {
        TotpSecretEncrypted = encryptedTotpSecret;
        TwoFactorAuthentication = true;
        ModifiedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Disable 2FA and clear TOTP secret.</summary>
    public void DisableTwoFactor()
    {
        TotpSecretEncrypted = null;
        TwoFactorAuthentication = false;
        ModifiedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Set password hash (BCrypt) for Ezofis login.</summary>
    public void SetPasswordHash(string hash) => PasswordHash = hash;

    /// <summary>Set login type (e.g. EZOFIS, GOOGLE, MICROSOFT).</summary>
    public void SetLoginType(string? loginType)
    {
        LoginType = string.IsNullOrWhiteSpace(loginType) ? null : loginType.Trim();
    }

    /// <summary>Set PIN hash for PIN-based login.</summary>
    public void SetPinHash(string hash) => PinHash = hash;

    /// <summary>Soft-delete the user.</summary>
    public void SoftDelete() => IsDeleted = true;

    /// <summary>Restore a soft-deleted user.</summary>
    public void Restore() => IsDeleted = false;
}
