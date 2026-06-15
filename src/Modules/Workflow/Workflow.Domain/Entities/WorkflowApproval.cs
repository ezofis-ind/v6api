using SaaSApp.SharedKernel.Domain;
using SaaSApp.SharedKernel.Domain.Interfaces;
using SaaSApp.Workflow.Domain.Enums;

namespace SaaSApp.Workflow.Domain.Entities;

/// <summary>Approval request for a workflow step. Each tenant has their own approvals.</summary>
public sealed class WorkflowApproval : Entity<Guid>, ITenantEntity
{
    public Guid TenantId { get; private set; }
    public Guid WorkflowInstanceId { get; private set; }
    public Guid StepInstanceId { get; private set; }
    public Guid RequestedBy { get; private set; }
    public Guid? AssignedToUserId { get; private set; }
    public string? AssignedToRole { get; private set; } // e.g. "Admin"
    public ApprovalStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? RespondedAtUtc { get; private set; }
    public Guid? RespondedBy { get; private set; }
    public string? Comments { get; private set; }

    private WorkflowApproval() { } // EF

    /// <summary>Create an approval request.</summary>
    public static WorkflowApproval Create(Guid tenantId, Guid workflowInstanceId, Guid stepInstanceId, Guid requestedBy, Guid? assignedToUserId = null, string? assignedToRole = null)
    {
        return new WorkflowApproval
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WorkflowInstanceId = workflowInstanceId,
            StepInstanceId = stepInstanceId,
            RequestedBy = requestedBy,
            AssignedToUserId = assignedToUserId,
            AssignedToRole = assignedToRole?.Trim(),
            Status = ApprovalStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>Approve the request.</summary>
    public void Approve(Guid approvedBy, string? comments = null)
    {
        if (Status != ApprovalStatus.Pending)
            throw new InvalidOperationException("Approval already processed.");
        Status = ApprovalStatus.Approved;
        RespondedAtUtc = DateTime.UtcNow;
        RespondedBy = approvedBy;
        Comments = comments?.Trim();
    }

    /// <summary>Reject the request.</summary>
    public void Reject(Guid rejectedBy, string? comments = null)
    {
        if (Status != ApprovalStatus.Pending)
            throw new InvalidOperationException("Approval already processed.");
        Status = ApprovalStatus.Rejected;
        RespondedAtUtc = DateTime.UtcNow;
        RespondedBy = rejectedBy;
        Comments = comments?.Trim();
    }

    /// <summary>Cancel the approval (workflow cancelled or step skipped).</summary>
    public void Cancel()
    {
        if (Status != ApprovalStatus.Pending)
            return; // Already processed
        Status = ApprovalStatus.Cancelled;
        RespondedAtUtc = DateTime.UtcNow;
    }
}
