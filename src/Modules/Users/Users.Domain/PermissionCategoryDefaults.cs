namespace SaaSApp.Users.Domain;

/// <summary>Default permission categories seeded into each tenant database.</summary>
public static class PermissionCategoryDefaults
{
    public static readonly Guid DashboardId = Guid.Parse("a1000001-0000-4000-8000-000000000006");
    public static readonly Guid WorkflowId = Guid.Parse("a1000001-0000-4000-8000-000000000001");
    public static readonly Guid FolderId = Guid.Parse("a1000001-0000-4000-8000-000000000002");
    public static readonly Guid TaskId = Guid.Parse("a1000001-0000-4000-8000-000000000003");
    public static readonly Guid WorkspaceId = Guid.Parse("a1000001-0000-4000-8000-000000000004");
    public static readonly Guid SettingsId = Guid.Parse("a1000001-0000-4000-8000-000000000005");

    public static IReadOnlyList<(Guid Id, string Key, string Name, int SortOrder)> All =>
    [
        (DashboardId, "dashboard", "Dashboard", 1),
        (WorkflowId, "workflow", "Workflow", 2),
        (FolderId, "folder", "Folder", 3),
        (TaskId, "task", "Task", 4),
        (WorkspaceId, "workspace", "Workspace", 5),
        (SettingsId, "settings", "Settings", 6),
    ];
}
