namespace SaaSApp.Users.Domain;

/// <summary>Fixed permission action columns shown in the role permissions matrix.</summary>
public static class PermissionActions
{
    public const string View = "view";
    public const string Create = "create";
    public const string Edit = "edit";
    public const string Delete = "delete";
    public const string Approve = "approve";
    public const string Export = "export";
    public const string Manage = "manage";

    private static readonly IReadOnlyList<PermissionActionDefinition> All =
    [
        new(View, "View"),
        new(Create, "Create"),
        new(Edit, "Edit"),
        new(Delete, "Delete"),
        new(Approve, "Approve"),
        new(Export, "Export"),
        new(Manage, "Manage"),
    ];

    private static readonly HashSet<string> ValidKeys = new(
        All.Select(a => a.Key),
        StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<PermissionActionDefinition> AllActions => All;

    public static bool IsValid(string actionKey) =>
        !string.IsNullOrWhiteSpace(actionKey) && ValidKeys.Contains(actionKey.Trim());
}

public sealed record PermissionActionDefinition(string Key, string Label);
