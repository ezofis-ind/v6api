using SaaSApp.Users.Application.Roles.Queries.ListPermissionCatalog;
using SaaSApp.Users.Domain.Entities;

namespace SaaSApp.Users.Application.Users;

internal static class UserExtendedResponseMapper
{
    public static UserExtendedResponse Map(User user, string? managerEmail = null) =>
        new()
        {
            Id = user.Id,
            Email = user.Email,
            DisplayName = user.DisplayName,
            Role = user.Role,
            FirstName = user.FirstName,
            LastName = user.LastName,
            AuthStrategy = user.AuthStrategy,
            UserName = user.LoginName,
            LoginType = user.LoginType,
            PasswordExpiryDays = user.PasswordExpiryDays,
            AccountExpiryDate = user.AccountExpiryDate,
            ForcePasswordResetOnLogin = ToYesNo(user.ForcePasswordResetOnLogin),
            JobTitle = user.JobTitle,
            EmployeeId = user.EmployeeId,
            Department = user.Department,
            BusinessUnit = user.BusinessUnit,
            Manager = managerEmail,
            Location = user.Location,
            Group = ParseGroups(user.GroupName),
            MfAuthentication = ToYesNo(user.TwoFactorAuthentication),
            MfaMethods = user.MfaMethods,
            PhoneNo = user.PhoneNo,
            Language = user.Language,
            CountryCode = user.CountryCode,
            AvatarPath = user.AvatarPath,
            UiPreference = user.UiPreference,
            SecondaryEmail = user.SecondaryEmail,
            UserType = user.UserType,
            IdCardPath = user.IdCardPath,
            SignaturePath = user.SignaturePath,
            CreatedAtUtc = user.CreatedAtUtc,
            CreatedBy = user.CreatedBy,
            ModifiedAtUtc = user.ModifiedAtUtc,
            ModifiedBy = user.ModifiedBy
        };

    public static UserExtendedResponse MapWithPermissions(
        User user,
        string? managerEmail,
        int permissionCount,
        IReadOnlyList<PermissionCategoryRow> permissionKeys)
    {
        var response = Map(user, managerEmail);
        response.PermissionCount = permissionCount;
        response.PermissionKeys = permissionKeys;
        return response;
    }

    public static string[]? ParseGroups(string? groupName)
    {
        if (string.IsNullOrWhiteSpace(groupName))
            return null;

        return groupName
            .Split(", ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }

    private static string ToYesNo(bool value) => value ? "Yes" : "No";
}
