using SaaSApp.SharedKernel.Domain;
using SaaSApp.SharedKernel.Domain.Interfaces;

namespace SaaSApp.Workflow.Domain.Entities;

/// <summary>Document checklist item for a workflow. Tracks required documents and their upload status.</summary>
public sealed class WorkflowDocument : Entity<Guid>, ITenantEntity
{
    public Guid TenantId { get; private set; }
    public Guid WorkflowId { get; private set; }
    public Guid? WorkflowInstanceId { get; private set; }
    public string FileName { get; private set; } = null!;
    public string? Description { get; private set; }
    public string? Type { get; private set; } // Document type: PDF, Image, Excel, etc.
    public int Status { get; private set; } // 0=Pending, 1=Uploaded, 2=Approved, 3=Rejected
    public bool IsMandatory { get; private set; }
    public string? FilePath { get; private set; }
    public DateTime? UploadedAt { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ModifiedAtUtc { get; private set; }
    public Guid CreatedBy { get; private set; }
    public Guid? ModifiedBy { get; private set; }
    public bool IsDeleted { get; private set; }

    private WorkflowDocument() { } // EF

    /// <summary>Create a document checklist item.</summary>
    public static WorkflowDocument Create(Guid tenantId, Guid workflowId, string fileName, bool isMandatory, Guid createdBy, Guid? workflowInstanceId = null, string? description = null, string? type = null)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("FileName is required.", nameof(fileName));

        return new WorkflowDocument
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WorkflowId = workflowId,
            WorkflowInstanceId = workflowInstanceId,
            FileName = fileName.Trim(),
            Description = description?.Trim(),
            Type = type?.Trim(),
            Status = 0,
            IsMandatory = isMandatory,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = createdBy
        };
    }

    /// <summary>Mark document as uploaded.</summary>
    public void MarkUploaded(string filePath, Guid modifiedBy)
    {
        FilePath = filePath?.Trim();
        Status = 1;
        UploadedAt = DateTime.UtcNow;
        ModifiedAtUtc = DateTime.UtcNow;
        ModifiedBy = modifiedBy;
    }

    /// <summary>Approve document.</summary>
    public void Approve(Guid modifiedBy)
    {
        Status = 2;
        ModifiedAtUtc = DateTime.UtcNow;
        ModifiedBy = modifiedBy;
    }

    /// <summary>Reject document.</summary>
    public void Reject(Guid modifiedBy)
    {
        Status = 3;
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
