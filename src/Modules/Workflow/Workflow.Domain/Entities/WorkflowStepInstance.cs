using SaaSApp.SharedKernel.Domain;
using SaaSApp.Workflow.Domain.Enums;

namespace SaaSApp.Workflow.Domain.Entities;

/// <summary>Workflow step instance (execution).</summary>
public sealed class WorkflowStepInstance : Entity<Guid>
{
    public Guid WorkflowInstanceId { get; private set; }
    public Guid WorkflowStepId { get; private set; }
    public string StepName { get; private set; } = null!; // Snapshot
    public StepType StepType { get; private set; }
    public int Order { get; private set; }
    public StepInstanceStatus Status { get; private set; }
    public Guid? AssignedToUserId { get; private set; }
    public string? AssignedToRole { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? StartedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public Guid? CompletedBy { get; private set; }
    public string? Result { get; private set; } // JSON: output data, approval decision, etc.
    public string? ErrorMessage { get; private set; }
    public string? ActivityId { get; private set; }
    public string? StageType { get; private set; }

    private WorkflowStepInstance() { } // EF

    /// <summary>Create a step instance.</summary>
    public static WorkflowStepInstance Create(
        Guid workflowInstanceId,
        Guid workflowStepId,
        string stepName,
        StepType stepType,
        int order,
        Guid? assignedToUserId = null,
        string? assignedToRole = null,
        string? activityId = null,
        string? stageType = null)
    {
        return new WorkflowStepInstance
        {
            Id = Guid.NewGuid(),
            WorkflowInstanceId = workflowInstanceId,
            WorkflowStepId = workflowStepId,
            StepName = stepName,
            StepType = stepType,
            Order = order,
            Status = StepInstanceStatus.Pending,
            AssignedToUserId = assignedToUserId,
            AssignedToRole = assignedToRole,
            ActivityId = activityId?.Trim(),
            StageType = stageType?.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>Start step execution.</summary>
    public void Start()
    {
        if (Status != StepInstanceStatus.Pending)
            throw new InvalidOperationException("Step already started.");
        Status = StepInstanceStatus.InProgress;
        StartedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Complete step execution.</summary>
    public void Complete(Guid completedBy, string? result = null)
    {
        if (Status != StepInstanceStatus.InProgress && Status != StepInstanceStatus.WaitingForApproval)
            throw new InvalidOperationException("Step not in progress.");
        Status = StepInstanceStatus.Completed;
        CompletedAtUtc = DateTime.UtcNow;
        CompletedBy = completedBy;
        Result = result?.Trim();
    }

    /// <summary>Fail step execution.</summary>
    public void Fail(string errorMessage)
    {
        Status = StepInstanceStatus.Failed;
        CompletedAtUtc = DateTime.UtcNow;
        ErrorMessage = errorMessage?.Trim();
    }

    /// <summary>Skip step (conditional branch not taken).</summary>
    public void Skip()
    {
        Status = StepInstanceStatus.Skipped;
        CompletedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Mark as waiting for approval.</summary>
    public void WaitForApproval()
    {
        if (StepType != StepType.Approval)
            throw new InvalidOperationException("Only approval steps can wait for approval.");
        Status = StepInstanceStatus.WaitingForApproval;
        StartedAtUtc ??= DateTime.UtcNow;
    }

    /// <summary>Cancel step (e.g., on rejection).</summary>
    public void Cancel()
    {
        Status = StepInstanceStatus.Failed;
        CompletedAtUtc = DateTime.UtcNow;
        ErrorMessage = "Cancelled/Rejected";
    }
}
