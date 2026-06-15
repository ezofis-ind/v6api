namespace SaaSApp.Workflow.Application.Forms;

public enum FormUpdateStatus
{
    Updated = 1,
    NotFound = 2,
    NameConflict = 3
}

public enum FormDeleteStatus
{
    Deleted = 1,
    NotFound = 2
}

public sealed record FormUpdateResult(FormUpdateStatus Status, string? FormId, string? Message);

public sealed record FormDeleteResult(FormDeleteStatus Status, string? Message);
