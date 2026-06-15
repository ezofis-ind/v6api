using SaaSApp.SharedKernel.Domain;
using SaaSApp.Workflow.Domain.Enums;

namespace SaaSApp.Workflow.Domain.Entities;

/// <summary>SLA tracking for a workflow instance. Tracks response and resolution deadlines.</summary>
public sealed class WorkflowInstanceSla : Entity<Guid>
{
    public Guid WorkflowInstanceId { get; private set; }
    public SlaPriority Priority { get; private set; }
    public DateTime ResponseDeadline { get; private set; } // When first step should start
    public DateTime ResolutionDeadline { get; private set; } // When workflow should complete
    public DateTime? EscalationDeadline { get; private set; }
    public DateTime? ResponseAchievedAt { get; private set; } // When first step started
    public DateTime? ResolutionAchievedAt { get; private set; } // When workflow completed
    public SlaStatus ResponseStatus { get; private set; }
    public SlaStatus ResolutionStatus { get; private set; }
    public bool IsEscalated { get; private set; }
    public DateTime? EscalatedAt { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private WorkflowInstanceSla() { } // EF

    /// <summary>Create SLA tracking for a workflow instance.</summary>
    public static WorkflowInstanceSla Create(Guid workflowInstanceId, SlaPriority priority, int responseTimeMinutes, int resolutionTimeMinutes, int? escalationTimeMinutes = null)
    {
        var now = DateTime.UtcNow;
        return new WorkflowInstanceSla
        {
            Id = Guid.NewGuid(),
            WorkflowInstanceId = workflowInstanceId,
            Priority = priority,
            ResponseDeadline = now.AddMinutes(responseTimeMinutes),
            ResolutionDeadline = now.AddMinutes(resolutionTimeMinutes),
            EscalationDeadline = escalationTimeMinutes.HasValue ? now.AddMinutes(escalationTimeMinutes.Value) : null,
            ResponseStatus = SlaStatus.OnTime,
            ResolutionStatus = SlaStatus.OnTime,
            IsEscalated = false,
            CreatedAtUtc = now
        };
    }

    /// <summary>Mark response achieved (first step started).</summary>
    public void MarkResponseAchieved()
    {
        if (ResponseAchievedAt.HasValue)
            return; // Already marked

        ResponseAchievedAt = DateTime.UtcNow;
        ResponseStatus = ResponseAchievedAt <= ResponseDeadline ? SlaStatus.Met : SlaStatus.Missed;
    }

    /// <summary>Mark resolution achieved (workflow completed).</summary>
    public void MarkResolutionAchieved()
    {
        if (ResolutionAchievedAt.HasValue)
            return; // Already marked

        ResolutionAchievedAt = DateTime.UtcNow;
        ResolutionStatus = ResolutionAchievedAt <= ResolutionDeadline ? SlaStatus.Met : SlaStatus.Missed;
    }

    /// <summary>Update SLA status based on current time (call periodically to check for breaches).</summary>
    public void UpdateStatus()
    {
        var now = DateTime.UtcNow;

        // Response SLA
        if (!ResponseAchievedAt.HasValue)
        {
            if (now > ResponseDeadline)
                ResponseStatus = SlaStatus.Breached;
            else if (now > ResponseDeadline.AddMinutes(-ResponseDeadline.Subtract(CreatedAtUtc).TotalMinutes * 0.2))
                ResponseStatus = SlaStatus.AtRisk; // 80% of time elapsed
        }

        // Resolution SLA
        if (!ResolutionAchievedAt.HasValue)
        {
            if (now > ResolutionDeadline)
                ResolutionStatus = SlaStatus.Breached;
            else if (now > ResolutionDeadline.AddMinutes(-ResolutionDeadline.Subtract(CreatedAtUtc).TotalMinutes * 0.2))
                ResolutionStatus = SlaStatus.AtRisk; // 80% of time elapsed
        }

        // Escalation
        if (!IsEscalated && EscalationDeadline.HasValue && now > EscalationDeadline.Value)
        {
            IsEscalated = true;
            EscalatedAt = now;
        }
    }

    /// <summary>Get time remaining until response deadline.</summary>
    public TimeSpan GetResponseTimeRemaining()
    {
        if (ResponseAchievedAt.HasValue)
            return TimeSpan.Zero;
        var remaining = ResponseDeadline - DateTime.UtcNow;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    /// <summary>Get time remaining until resolution deadline.</summary>
    public TimeSpan GetResolutionTimeRemaining()
    {
        if (ResolutionAchievedAt.HasValue)
            return TimeSpan.Zero;
        var remaining = ResolutionDeadline - DateTime.UtcNow;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }
}
