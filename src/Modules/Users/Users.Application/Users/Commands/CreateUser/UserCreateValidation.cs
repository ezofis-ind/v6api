using System.Net.Mail;
using SaaSApp.Users.Domain.Entities;

namespace SaaSApp.Users.Application.Users.Commands.CreateUser;

internal static class UserCreateValidation
{
    public const int DefaultPasswordExpiryDays = 90;

    public const string LoginTypePassword = "Password";
    public const string LoginTypeGoogleSso = "GoogleSSO";
    public const string LoginTypeMsEntraId = "MS Entra ID";
    public const string LoginTypeLdapAd = "LDAP/AD";

    public const string MfaMethodEmailOtp = "Email OTP";
    public const string MfaMethodMobileOtp = "Mobile OTP";
    public const string MfaMethodAuthenticatorOtp = "Authenticator OTP";

    private static readonly HashSet<string> AllowedLoginTypes = new(StringComparer.Ordinal)
    {
        LoginTypePassword,
        LoginTypeGoogleSso,
        LoginTypeMsEntraId,
        LoginTypeLdapAd
    };

    private static readonly HashSet<string> AllowedMfaMethods = new(StringComparer.Ordinal)
    {
        MfaMethodEmailOtp,
        MfaMethodMobileOtp,
        MfaMethodAuthenticatorOtp
    };

    public static string ResolveRole(string? role) =>
        string.IsNullOrWhiteSpace(role) ? User.RoleTenantUser : role.Trim();

    public static string ResolveLoginType(string? loginType) =>
        string.IsNullOrWhiteSpace(loginType) ? LoginTypePassword : loginType.Trim();

    public static string ResolveAuthStrategy(string? authStrategy) =>
        string.IsNullOrWhiteSpace(authStrategy) ? User.AuthStrategyEzofis : authStrategy.Trim();

    public static int ResolvePasswordExpiryDays(int? passwordExpiryDays) =>
        passwordExpiryDays ?? DefaultPasswordExpiryDays;

    public static bool? TryParseYesNo(string? value, out bool result)
    {
        result = false;
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim();
        if (string.Equals(normalized, "Yes", StringComparison.OrdinalIgnoreCase))
        {
            result = true;
            return true;
        }

        if (string.Equals(normalized, "No", StringComparison.OrdinalIgnoreCase))
        {
            result = false;
            return true;
        }

        return false;
    }

    public static bool IsAllowedLoginType(string loginType) => AllowedLoginTypes.Contains(loginType);

    public static bool IsAllowedMfaMethod(string mfaMethod) => AllowedMfaMethods.Contains(mfaMethod);

    public static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        try
        {
            _ = new MailAddress(email.Trim());
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static IReadOnlyList<string> NormalizeGroupNames(IEnumerable<string>? groups)
    {
        if (groups == null)
            return [];

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var normalized = new List<string>();
        foreach (var group in groups)
        {
            if (string.IsNullOrWhiteSpace(group))
                continue;

            var trimmed = group.Trim();
            if (seen.Add(trimmed))
                normalized.Add(trimmed);
        }

        return normalized;
    }

    public const int MaxGroupNameLength = 128;

    public static string? FormatGroupNamesForStorage(IReadOnlyList<string> groupNames) =>
        groupNames.Count == 0 ? null : string.Join(", ", groupNames);

    public static DateTime ResolveAccountExpiryDate(
        DateTime? requestedAccountExpiryDate,
        int passwordExpiryDays,
        out string? error)
    {
        error = null;
        var utcToday = DateTime.UtcNow.Date;
        var minimumExpiryDate = utcToday.AddDays(passwordExpiryDays);

        if (requestedAccountExpiryDate == null)
            return minimumExpiryDate;

        var accountExpiryDate = requestedAccountExpiryDate.Value.Date;
        if (accountExpiryDate <= minimumExpiryDate)
        {
            error = $"accountExpiryDate must be after {minimumExpiryDate:yyyy-MM-dd}.";
            return accountExpiryDate;
        }

        return accountExpiryDate;
    }
}
