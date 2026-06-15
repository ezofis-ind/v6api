using SaaSApp.SharedKernel.Domain;
using SaaSApp.SharedKernel.Domain.Interfaces;

namespace SaaSApp.Workflow.Domain.Entities;

/// <summary>Email sent/received for a workflow instance.</summary>
public sealed class WorkflowEmail : Entity<Guid>, ITenantEntity
{
    public Guid TenantId { get; private set; }
    public Guid WorkflowInstanceId { get; private set; }
    public Guid? StepInstanceId { get; private set; }
    public string EmailType { get; private set; } = null!; // Sent, Received, Notification
    public int? EzMailId { get; private set; } // External email system ID
    public string? MsgFileName { get; private set; }
    public string Email { get; private set; } = null!;
    public string? Subject { get; private set; }
    public string? Body { get; private set; }
    public int AttachmentCount { get; private set; }
    public string? AttachmentJson { get; private set; } // JSON array of attachments
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ModifiedAtUtc { get; private set; }
    public Guid CreatedBy { get; private set; }
    public Guid? ModifiedBy { get; private set; }
    public bool IsDeleted { get; private set; }

    private WorkflowEmail() { } // EF

    /// <summary>Create a workflow email record.</summary>
    public static WorkflowEmail Create(Guid tenantId, Guid workflowInstanceId, string emailType, string email, Guid createdBy, Guid? stepInstanceId = null, string? subject = null, string? body = null, int? ezMailId = null, string? msgFileName = null, int attachmentCount = 0, string? attachmentJson = null)
    {
        if (string.IsNullOrWhiteSpace(emailType))
            throw new ArgumentException("EmailType is required.", nameof(emailType));
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required.", nameof(email));

        return new WorkflowEmail
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WorkflowInstanceId = workflowInstanceId,
            StepInstanceId = stepInstanceId,
            EmailType = emailType.Trim(),
            EzMailId = ezMailId,
            MsgFileName = msgFileName?.Trim(),
            Email = email.Trim(),
            Subject = subject?.Trim(),
            Body = body?.Trim(),
            AttachmentCount = attachmentCount,
            AttachmentJson = attachmentJson?.Trim(),
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
