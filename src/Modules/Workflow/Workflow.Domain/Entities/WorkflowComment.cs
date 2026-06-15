using SaaSApp.SharedKernel.Domain;
using SaaSApp.SharedKernel.Domain.Interfaces;

namespace SaaSApp.Workflow.Domain.Entities;

/// <summary>Comment on a workflow instance or step. Supports internal and external comments.</summary>
public sealed class WorkflowComment : Entity<Guid>, ITenantEntity
{
    public Guid TenantId { get; private set; }
    public Guid WorkflowInstanceId { get; private set; }
    public Guid? StepInstanceId { get; private set; }
    public string Comments { get; private set; } = null!;
    public string? ExternalCommentsBy { get; private set; } // External user/system name
    public int ShowTo { get; private set; } // Visibility: 0=Internal, 1=Customer, 2=All
    public string? EmbedJson { get; private set; } // Embedded content (images, links, etc.)
    public bool EmbedStatus { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ModifiedAtUtc { get; private set; }
    public Guid CreatedBy { get; private set; }
    public Guid? ModifiedBy { get; private set; }
    public bool IsDeleted { get; private set; }

    private WorkflowComment() { } // EF

    /// <summary>Create a workflow comment.</summary>
    public static WorkflowComment Create(Guid tenantId, Guid workflowInstanceId, string comments, Guid createdBy, Guid? stepInstanceId = null, string? externalCommentsBy = null, int showTo = 0, string? embedJson = null, bool embedStatus = false)
    {
        if (string.IsNullOrWhiteSpace(comments))
            throw new ArgumentException("Comments cannot be empty.", nameof(comments));

        return new WorkflowComment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WorkflowInstanceId = workflowInstanceId,
            StepInstanceId = stepInstanceId,
            Comments = comments.Trim(),
            ExternalCommentsBy = externalCommentsBy?.Trim(),
            ShowTo = showTo,
            EmbedJson = embedJson?.Trim(),
            EmbedStatus = embedStatus,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = createdBy
        };
    }

    /// <summary>Update comment.</summary>
    public void Update(string comments, Guid modifiedBy, string? embedJson = null, bool? embedStatus = null)
    {
        if (!string.IsNullOrWhiteSpace(comments))
            Comments = comments.Trim();
        if (embedJson != null)
            EmbedJson = embedJson.Trim();
        if (embedStatus.HasValue)
            EmbedStatus = embedStatus.Value;
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
