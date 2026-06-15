using SaaSApp.SharedKernel.Domain;
using SaaSApp.SharedKernel.Domain.Interfaces;

namespace SaaSApp.Workflow.Domain.Entities;

/// <summary>Digital signature for a workflow instance or step.</summary>
public sealed class WorkflowSignature : Entity<Guid>, ITenantEntity
{
    public Guid TenantId { get; private set; }
    public Guid WorkflowInstanceId { get; private set; }
    public Guid? StepInstanceId { get; private set; }
    public string FileName { get; private set; } = null!;
    public string? FilePath { get; private set; }
    public Guid SignedBy { get; private set; }
    public DateTime SignedAtUtc { get; private set; }
    public string? SignatureData { get; private set; } // Base64 or path
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ModifiedAtUtc { get; private set; }
    public Guid CreatedBy { get; private set; }
    public Guid? ModifiedBy { get; private set; }
    public bool IsDeleted { get; private set; }

    private WorkflowSignature() { } // EF

    /// <summary>Create a workflow signature.</summary>
    public static WorkflowSignature Create(Guid tenantId, Guid workflowInstanceId, string fileName, Guid signedBy, Guid createdBy, Guid? stepInstanceId = null, string? filePath = null, string? signatureData = null)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("FileName is required.", nameof(fileName));

        return new WorkflowSignature
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WorkflowInstanceId = workflowInstanceId,
            StepInstanceId = stepInstanceId,
            FileName = fileName.Trim(),
            FilePath = filePath?.Trim(),
            SignedBy = signedBy,
            SignedAtUtc = DateTime.UtcNow,
            SignatureData = signatureData?.Trim(),
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
