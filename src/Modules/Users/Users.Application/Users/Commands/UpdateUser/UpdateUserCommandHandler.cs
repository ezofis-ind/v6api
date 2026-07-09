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
            return new UpdateUserCommandResult(Found: false);

        if (request.ForcePasswordResetOnLogin != null
            && UserCreateValidation.TryParseYesNo(request.ForcePasswordResetOnLogin, out _) != true)
            return Fail("forcePasswordResetOnLogin must be Yes or No.");

        if (request.MfAuthentication != null
            && UserCreateValidation.TryParseYesNo(request.MfAuthentication, out _) != true)
            return Fail("MFAuthentication must be Yes or No.");

        string? loginType = null;
        if (request.LoginType != null)
        {
            loginType = UserCreateValidation.ResolveLoginType(request.LoginType);
            if (!UserCreateValidation.IsAllowedLoginType(loginType))
                return Fail("LoginType must be one of: Password, GoogleSSO, MS Entra ID, LDAP/AD.");
        }

        if (!string.IsNullOrWhiteSpace(request.MfaMethods))
        {
            var mfaMethod = request.MfaMethods.Trim();
            if (!UserCreateValidation.IsAllowedMfaMethod(mfaMethod))
                return Fail("MFA Methods must be one of: Email OTP, Mobile OTP, Authenticator OTP.");
        }

        if (!string.IsNullOrWhiteSpace(request.SecondaryEmail)
            && !UserCreateValidation.IsValidEmail(request.SecondaryEmail))
            return Fail("secondaryEmail is not a valid email address.");

        var passwordExpiryDays = request.PasswordExpiryDays ?? user.PasswordExpiryDays;
        DateTime? accountExpiryDate = null;
        if (request.AccountExpiryDate != null)
        {
            accountExpiryDate = UserCreateValidation.ResolveAccountExpiryDate(
                request.AccountExpiryDate,
                passwordExpiryDays,
                out var accountExpiryError);
            if (accountExpiryError != null)
                return Fail(accountExpiryError);
        }

        string? email = null;
        var emailChanged = false;
        if (request.Email != null)
        {
            email = request.Email.Trim();
            if (string.IsNullOrWhiteSpace(email))
                return Fail("email cannot be empty.");

            var existing = await _userRepository.GetByEmailAsync(email, cancellationToken);
            if (existing != null && existing.Id != user.Id)
                return Fail("A user with this email already exists.");

            emailChanged = !string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase);
        }

        Guid? managerId = null;
        var managerProvided = request.Manager != null;
        if (managerProvided)
        {
            if (string.IsNullOrWhiteSpace(request.Manager))
            {
                managerId = null;
            }
            else
            {
                var manager = await _userRepository.FindByEmailOrDisplayNameAsync(request.Manager, cancellationToken);
                if (manager == null)
                    return Fail($"Manager '{request.Manager.Trim()}' was not found.");
                managerId = manager.Id;
            }
        }

        string? groupNameValue = null;
        IReadOnlyList<string> groupNames = [];
        if (request.Groups != null)
        {
            groupNames = UserCreateValidation.NormalizeGroupNames(request.Groups);
            if (groupNames.Count > 0)
                await _usersSchemaEnsurer.EnsureGroupsTablesAsync(cancellationToken);

            groupNameValue = UserCreateValidation.FormatGroupNamesForStorage(groupNames);
            if (groupNameValue?.Length > UserCreateValidation.MaxGroupNameLength)
                return Fail("Combined group names exceed 128 characters.");
        }

        bool? forcePasswordResetOnLogin = null;
        if (request.ForcePasswordResetOnLogin != null
            && UserCreateValidation.TryParseYesNo(request.ForcePasswordResetOnLogin, out var parsedForceReset) == true)
            forcePasswordResetOnLogin = parsedForceReset;

        bool? twoFactorAuthentication = null;
        if (request.MfAuthentication != null
            && UserCreateValidation.TryParseYesNo(request.MfAuthentication, out var parsedMfa) == true)
            twoFactorAuthentication = parsedMfa;

        var roleChanged = false;
        string? role = null;
        if (request.Role != null)
        {
            role = request.Role.Trim();
            roleChanged = !string.Equals(user.Role, role, StringComparison.OrdinalIgnoreCase);
        }

        user.Update(
            displayName: request.DisplayName,
            role: role,
            firstName: request.FirstName,
            lastName: request.LastName,
            phoneNo: request.PhoneNo,
            department: request.Department,
            jobTitle: request.JobTitle,
            language: request.Language,
            countryCode: request.CountryCode,
            avatarPath: request.AvatarPath,
            uiPreference: request.UiPreference,
            email: email,
            authStrategy: request.AuthStrategy != null ? UserCreateValidation.ResolveAuthStrategy(request.AuthStrategy) : null,
            loginName: request.UserName,
            loginType: loginType,
            passwordExpiryDays: request.PasswordExpiryDays,
            accountExpiryDate: request.AccountExpiryDate != null ? accountExpiryDate : null,
            forcePasswordResetOnLogin: forcePasswordResetOnLogin,
            twoFactorAuthentication: twoFactorAuthentication,
            mfaMethods: request.MfaMethods,
            employeeId: request.EmployeeId,
            businessUnit: request.BusinessUnit,
            managerId: managerProvided ? managerId : null,
            applyManagerId: managerProvided,
            location: request.Location,
            groupName: request.Groups != null ? groupNameValue : null,
            applyGroupName: request.Groups != null,
            userType: request.UserType,
            secondaryEmail: request.SecondaryEmail,
            idCardPath: request.IdCardPath,
            signaturePath: request.SignaturePath,
            modifiedBy: request.ModifiedBy);

        if (!string.IsNullOrWhiteSpace(request.Password))
            user.SetPasswordHash(BCrypt.Net.BCrypt.HashPassword(request.Password.Trim()));

        if (request.Groups != null)
        {
            var tenantId = _tenantContext.TenantId
                ?? throw new InvalidOperationException("TenantId is required to update user groups.");

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

        _userRepository.Update(user);

        string? registryEmail = null;
        string? registryRole = null;
        if (emailChanged || roleChanged)
        {
            registryEmail = user.Email;
            registryRole = user.Role;
        }

        return new UpdateUserCommandResult(
            RegistryEmail: registryEmail,
            RegistryRole: registryRole);
    }

    private static UpdateUserCommandResult Fail(string error, int statusCode = 400) =>
        new(Success: false, Error: error, StatusCode: statusCode);
}
