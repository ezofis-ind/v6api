using SaaSApp.SharedKernel.Domain;
using SaaSApp.SharedKernel.Domain.Interfaces;

namespace SaaSApp.Workflow.Domain.Entities;

/// <summary>Attachment/addon linked to a workflow instance or step.</summary>
public sealed class WorkflowAttachment : Entity<Guid>, ITenantEntity
{
    public Guid TenantId { get; private set; }
    public Guid WorkflowInstanceId { get; private set; }
    public Guid? StepInstanceId { get; private set; }
    public int? RepositoryId { get; private set; } // External repository/storage ID
    public int? ItemId { get; private set; } // External item ID
    public string? FormJsonId { get; private set; } // Form data reference
    public string? FileName { get; private set; }
    public string? FilePath { get; private set; }
    public long? FileSize { get; private set; }
    public string? ContentType { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ModifiedAtUtc { get; private set; }
    public Guid CreatedBy { get; private set; }
    public Guid? ModifiedBy { get; private set; }
    public bool IsDeleted { get; private set; }

    private WorkflowAttachment() { } // EF

    /// <summary>Create a workflow attachment.</summary>
    public static WorkflowAttachment Create(Guid tenantId, Guid workflowInstanceId, Guid createdBy, string? fileName = null, string? filePath = null, Guid? stepInstanceId = null, int? repositoryId = null, int? itemId = null, string? formJsonId = null, long? fileSize = null, string? contentType = null)
    {
        return new WorkflowAttachment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WorkflowInstanceId = workflowInstanceId,
            StepInstanceId = stepInstanceId,
            RepositoryId = repositoryId,
            ItemId = itemId,
            FormJsonId = formJsonId?.Trim(),
            FileName = fileName?.Trim(),
            FilePath = filePath?.Trim(),
            FileSize = fileSize,
            ContentType = contentType?.Trim(),
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = createdBy
        };
    }

    /// <summary>Soft delete.</summary>
    public void SoftDelete()
    {
        IsDeleted = true;
        ModifiedAtUtc = DateTime.UtcNow;
    }
}
