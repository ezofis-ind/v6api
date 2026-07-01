namespace SaaSApp.Workflow.Infrastructure.Options;

public sealed class FormMasterFileImportOptions
{
    public const string SectionName = "FormMasterFileImport";

    public bool Enabled { get; set; } = true;

    /// <summary>When set, enqueue Hangfire job to POST import payload to this Python URL.</summary>
    public string? PythonServiceUrl { get; set; }

    public bool UseHangfirePython { get; set; } = true;

    /// <summary>Legacy v5 blob queue JSON (external worker). Can run alongside Python when true.</summary>
    public bool QueueBlobEnabled { get; set; }

    public string QueueBlobPathPrefix { get; set; } = "ezPackages/MasterExcel";

    public int TimeoutMinutes { get; set; } = 30;

    public string NotificationCategory { get; set; } = "WORKFLOW";
}
