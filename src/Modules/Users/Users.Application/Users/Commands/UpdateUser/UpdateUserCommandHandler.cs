using MediatR;
using SaaSApp.Users.Application.Contracts;
using SaaSApp.Users.Application.Users.Commands.CreateUser;
using SaaSApp.Users.Domain.Entities;

namespace SaaSApp.Users.Application.Users.Commands.UpdateUser;

public sealed class UpdateUserCommandHandler : IRequestHandler<UpdateUserCommand, UpdateUserCommandResult>
{
    private readonly IUserRepository _userRepository;
    private readonly IGroupRepository _groupRepository;
    private readonly IUsersSchemaEnsurer _usersSchemaEnsurer;
    private readonly ITenantContext _tenantContext;

    public UpdateUserCommandHandler(
        IUserRepository userRepository,
        IGroupRepository groupRepository,
        IUsersSchemaEnsurer usersSchemaEnsurer,
        ITenantContext tenantContext)
    {
        _userRepository = userRepository;
        _groupRepository = groupRepository;
        _usersSchemaEnsurer = usersSchemaEnsurer;
        _tenantContext = tenantContext;
    }

    public async Task<UpdateUserCommandResult> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user == null)
            return new UpdateUserCommandResult(Success: false, Found: false, StatusCode: 404);

        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("TenantId is required to update a user.");

        bool? forcePasswordResetOnLogin = null;
        if (request.ForcePasswordResetOnLogin != null)
        {
            if (UserCreateValidation.TryParseYesNo(request.ForcePasswordResetOnLogin, out var parsedForceReset) != true)
                return Fail("forcePasswordResetOnLogin must be Yes or No.");
            forcePasswordResetOnLogin = parsedForceReset;
        }

        bool? twoFactorAuthentication = null;
        if (request.MfAuthentication != null)
        {
            if (UserCreateValidation.TryParseYesNo(request.MfAuthentication, out var parsedMfa) != true)
                return Fail("MFAuthentication must be Yes or No.");
            twoFactorAuthentication = parsedMfa;
        }

        string? loginType = null;
        if (request.LoginType != null)
        {
            loginType = UserCreateValidation.ResolveLoginType(request.LoginType);
            if (!UserCreateValidation.IsAllowedLoginType(loginType))
                return Fail("LoginType must be one of: Password, GoogleSSO, MS Entra ID, LDAP/AD.");
        }

        if (request.MfaMethods != null)
        {
            var mfaMethod = request.MfaMethods.Trim();
            if (!string.IsNullOrWhiteSpace(mfaMethod) && !UserCreateValidation.IsAllowedMfaMethod(mfaMethod))
                return Fail("MFA Methods must be one of: Email OTP, Mobile OTP, Authenticator OTP.");
        }

        if (request.SecondaryEmail != null
            && !string.IsNullOrWhiteSpace(request.SecondaryEmail)
            && !UserCreateValidation.IsValidEmail(request.SecondaryEmail))
            return Fail("secondaryEmail is not a valid email address.");

        DateTime? resolvedAccountExpiryDate = null;
        if (request.AccountExpiryDate != null)
        {
            var passwordExpiryDays = request.PasswordExpiryDays ?? user.PasswordExpiryDays;
            resolvedAccountExpiryDate = UserCreateValidation.ResolveAccountExpiryDate(
                request.AccountExpiryDate,
                passwordExpiryDays,
                out var accountExpiryError);
            if (accountExpiryError != null)
                return Fail(accountExpiryError);
        }

        var emailChanged = false;
        string? updatedEmail = null;
        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            updatedEmail = request.Email.Trim();
            var existing = await _userRepository.GetByEmailAsync(updatedEmail, cancellationToken);
            if (existing != null && existing.Id != user.Id)
                return Fail("A user with this email already exists.");
            emailChanged = !string.Equals(user.Email, updatedEmail, StringComparison.Ordinal);
        }

        var roleChanged = false;
        string? resolvedRole = null;
        if (request.Role != null)
        {
            resolvedRole = UserCreateValidation.ResolveRole(request.Role);
            roleChanged = !string.Equals(user.Role, resolvedRole, StringComparison.Ordinal);
        }

        Guid? managerId = null;
        var applyManagerId = false;
        if (request.Manager != null)
        {
            applyManagerId = true;
            if (!string.IsNullOrWhiteSpace(request.Manager))
            {
                var manager = await _userRepository.FindByEmailOrDisplayNameAsync(request.Manager, cancellationToken);
                if (manager == null)
                    return Fail($"Manager '{request.Manager.Trim()}' was not found.");
                managerId = manager.Id;
            }
        }

        IReadOnlyList<string>? groupNames = null;
        string? groupNameValue = null;
        if (request.Groups != null)
        {
            groupNames = UserCreateValidation.NormalizeGroupNames(request.Groups);
            await _usersSchemaEnsurer.EnsureGroupsTablesAsync(cancellationToken);
            groupNameValue = UserCreateValidation.FormatGroupNamesForStorage(groupNames);
            if (groupNameValue?.Length > UserCreateValidation.MaxGroupNameLength)
                return Fail("Combined group names exceed 128 characters.");
        }

        if (!string.IsNullOrWhiteSpace(request.Password))
            user.SetPasswordHash(BCrypt.Net.BCrypt.HashPassword(request.Password.Trim()));

        if (emailChanged && updatedEmail != null)
            user.SetEmail(updatedEmail);

        user.Update(
            request.DisplayName,
            resolvedRole,
            request.FirstName,
            request.LastName,
            request.PhoneNo,
            request.Department,
            request.JobTitle,
            request.Language,
            request.CountryCode,
            request.AvatarPath,
            request.UiPreference,
            request.AuthStrategy != null ? UserCreateValidation.ResolveAuthStrategy(request.AuthStrategy) : null,
            request.UserName,
            loginType,
            request.PasswordExpiryDays,
            resolvedAccountExpiryDate,
            forcePasswordResetOnLogin,
            twoFactorAuthentication,
            request.MfaMethods,
            request.EmployeeId,
            request.BusinessUnit,
            managerId,
            applyManagerId,
            request.Location,
            groupNameValue,
            request.UserType,
            request.SecondaryEmail,
            request.IdCardPath,
            request.SignaturePath,
            request.ModifiedBy);

        _userRepository.Update(user);

        if (groupNames != null)
        {
            foreach (var groupName in groupNames)
            {
                var group = await _groupRepository.GetByNameAsync(groupName, cancellationToken);
                if (group == null)
                {
                    group = Group.Create(tenantId, groupName);
                    group.AssignUsers([user.Id]);
                    await _groupRepository.AddAsync(group, cancellationToken);
                }
                else
                {
                    await _groupRepository.AddMemberAsync(group.Id, user.Id, cancellationToken);
                }
            }
        }

        var registrySyncRequired = emailChanged || roleChanged;
        return new UpdateUserCommandResult(
            Success: true,
            StatusCode: 204,
            RegistrySyncRequired: registrySyncRequired,
            RegistryEmail: registrySyncRequired ? (updatedEmail ?? user.Email) : null,
            RegistryRole: registrySyncRequired ? (resolvedRole ?? user.Role) : null);
    }

    private static UpdateUserCommandResult Fail(string error, int statusCode = 400) =>
        new(Success: false, Error: error, StatusCode: statusCode);
}
