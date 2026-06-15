using SaaSApp.SharedKernel.Domain;
using SaaSApp.SharedKernel.Domain.Interfaces;

namespace SaaSApp.Workflow.Domain.Entities;

/// <summary>PDF annotation for a workflow instance.</summary>
public sealed class WorkflowPdfAnnotation : Entity<Guid>, ITenantEntity
{
    public Guid TenantId { get; private set; }
    public Guid WorkflowInstanceId { get; private set; }
    public Guid? StepInstanceId { get; private set; }
    public int? RepositoryId { get; private set; }
    public int? ItemId { get; private set; }
    public int AnnotationStatus { get; private set; } // 0=Pending, 1=InProgress, 2=Completed
    public string? SettingsJson { get; private set; } // Annotation settings/config
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ModifiedAtUtc { get; private set; }
    public Guid CreatedBy { get; private set; }
    public Guid? ModifiedBy { get; private set; }
    public bool IsDeleted { get; private set; }

    private WorkflowPdfAnnotation() { } // EF

    /// <summary>Create a PDF annotation record.</summary>
    public static WorkflowPdfAnnotation Create(Guid tenantId, Guid workflowInstanceId, Guid createdBy, Guid? stepInstanceId = null, int? repositoryId = null, int? itemId = null, int annotationStatus = 0, string? settingsJson = null)
    {
        return new WorkflowPdfAnnotation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WorkflowInstanceId = workflowInstanceId,
            StepInstanceId = stepInstanceId,
            RepositoryId = repositoryId,
            ItemId = itemId,
            AnnotationStatus = annotationStatus,
            SettingsJson = settingsJson?.Trim(),
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = createdBy
        };
    }

    /// <summary>Update annotation status.</summary>
    public void UpdateStatus(int status, Guid modifiedBy, string? settingsJson = null)
    {
        AnnotationStatus = status;
        if (settingsJson != null)
            SettingsJson = settingsJson.Trim();
        ModifiedAtUtc = DateTime.UtcNow;
        ModifiedBy = modifiedBy;
    }

    /// <summary>Soft delete.</summary>
    public void SoftDelete()
    {
        IsDeleted = true;
        ModifiedAtUtc = DateTime.UtcNow;
    }
}
