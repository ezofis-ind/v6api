namespace SaaSApp.Users.Application.Roles;

/// <summary>Permission category visibility item for role/user read responses.</summary>
public sealed record PermissionKeyItem(string Key, string Name, bool Visible);
