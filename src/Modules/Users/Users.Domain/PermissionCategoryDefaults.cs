namespace SaaSApp.Users.Domain;

/// <summary>Default permission categories seeded into each tenant database.</summary>
public static class PermissionCategoryDefaults
{
    public static readonly Guid DashboardId = Guid.Parse("a1000001-0000-4000-8000-000000000001");
    public static readonly Guid InvoicesId = Guid.Parse("a1000001-0000-4000-8000-000000000002");
    public static readonly Guid OcrDocumentProcessingId = Guid.Parse("a1000001-0000-4000-8000-000000000003");
    public static readonly Guid WorkflowApprovalsId = Guid.Parse("a1000001-0000-4000-8000-000000000004");
    public static readonly Guid ReportsAnalyticsId = Guid.Parse("a1000001-0000-4000-8000-000000000005");
    public static readonly Guid UserManagementId = Guid.Parse("a1000001-0000-4000-8000-000000000006");
    public static readonly Guid IntegrationsId = Guid.Parse("a1000001-0000-4000-8000-000000000007");
    public static readonly Guid SystemSettingsId = Guid.Parse("a1000001-0000-4000-8000-000000000008");

    public static IReadOnlyList<(Guid Id, string Key, string Name, int SortOrder)> All =>
    [
        (DashboardId, "dashboard", "Dashboard", 1),
        (InvoicesId, "invoices", "Invoices", 2),
        (OcrDocumentProcessingId, "ocr-document-processing", "OCR / Document Processing", 3),
        (WorkflowApprovalsId, "workflow-approvals", "Workflow & Approvals", 4),
        (ReportsAnalyticsId, "reports-analytics", "Reports & Analytics", 5),
        (UserManagementId, "user-management", "User Management", 6),
        (IntegrationsId, "integrations", "Integrations", 7),
        (SystemSettingsId, "system-settings", "System Settings", 8),
    ];
}
