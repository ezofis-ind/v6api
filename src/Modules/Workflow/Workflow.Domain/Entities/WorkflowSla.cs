using SaaSApp.SharedKernel.Domain;
using SaaSApp.SharedKernel.Domain.Interfaces;
using SaaSApp.Workflow.Domain.Enums;

namespace SaaSApp.Workflow.Domain.Entities;

/// <summary>SLA policy for a workflow. Defines response and resolution time targets.</summary>
public sealed class WorkflowSla : Entity<Guid>, ITenantEntity
{
    public Guid TenantId { get; private set; }
    public Guid WorkflowId { get; private set; }
    public SlaPriority Priority { get; private set; }
    public int ResponseTimeMinutes { get; private set; } // Time to start first step
    public int ResolutionTimeMinutes { get; private set; } // Time to complete workflow
    public int? EscalationTimeMinutes { get; private set; } // Time before escalation
    public Guid? EscalateToUserId { get; private set; }
    public string? EscalateToRole { get; private set; } // e.g. "Admin"
    public bool SendNotificationOnBreach { get; private set; }
    public string? NotificationEmails { get; private set; } // Comma-separated
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ModifiedAtUtc { get; private set; }

    private WorkflowSla() { } // EF

    /// <summary>Create an SLA policy for a workflow.</summary>
    public static WorkflowSla Create(Guid tenantId, Guid workflowId, SlaPriority priority, int responseTimeMinutes, int resolutionTimeMinutes, int? escalationTimeMinutes = null, Guid? escalateToUserId = null, string? escalateToRole = null, bool sendNotificationOnBreach = true, string? notificationEmails = null)
    {
        if (responseTimeMinutes <= 0)
            throw new ArgumentException("Response time must be positive.", nameof(responseTimeMinutes));
        if (resolutionTimeMinutes <= 0)
            throw new ArgumentException("Resolution time must be positive.", nameof(resolutionTimeMinutes));
        if (escalationTimeMinutes.HasValue && escalationTimeMinutes.Value <= 0)
            throw new ArgumentException("Escalation time must be positive.", nameof(escalationTimeMinutes));

        return new WorkflowSla
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WorkflowId = workflowId,
            Priority = priority,
            ResponseTimeMinutes = responseTimeMinutes,
            ResolutionTimeMinutes = resolutionTimeMinutes,
            EscalationTimeMinutes = escalationTimeMinutes,
            EscalateToUserId = escalateToUserId,
            EscalateToRole = escalateToRole?.Trim(),
            SendNotificationOnBreach = sendNotificationOnBreach,
            NotificationEmails = notificationEmails?.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>Update SLA policy.</summary>
    public void Update(SlaPriority? priority, int? responseTimeMinutes, int? resolutionTimeMinutes, int? escalationTimeMinutes, Guid? escalateToUserId, string? escalateToRole, bool? sendNotificationOnBreach, string? notificationEmails)
    {
        if (priority.HasValue)
            Priority = priority.Value;
        if (responseTimeMinutes.HasValue && responseTimeMinutes.Value > 0)
            ResponseTimeMinutes = responseTimeMinutes.Value;
        if (resolutionTimeMinutes.HasValue && resolutionTimeMinutes.Value > 0)
            ResolutionTimeMinutes = resolutionTimeMinutes.Value;
        if (escalationTimeMinutes.HasValue)
            EscalationTimeMinutes = escalationTimeMinutes.Value > 0 ? escalationTimeMinutes.Value : null;
        if (escalateToUserId.HasValue)
            EscalateToUserId = escalateToUserId;
        if (escalateToRole != null)
            EscalateToRole = escalateToRole.Trim();
        if (sendNotificationOnBreach.HasValue)
            SendNotificationOnBreach = sendNotificationOnBreach.Value;
        if (notificationEmails != null)
            NotificationEmails = notificationEmails.Trim();
        ModifiedAtUtc = DateTime.UtcNow;
    }
}
