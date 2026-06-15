using SaaSApp.SharedKernel.Domain;
using SaaSApp.Workflow.Domain.Enums;

namespace SaaSApp.Workflow.Domain.Entities;

/// <summary>Step definition in a workflow.</summary>
public sealed class WorkflowStep : Entity<Guid>
{
    public Guid WorkflowId { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public StepType StepType { get; private set; }
    public int Order { get; private set; }
    public string? Config { get; private set; } // JSON: approval users, API endpoint, condition expression, etc.
    public bool IsRequired { get; private set; }
    public Guid? AssignedToUserId { get; private set; }
    public string? AssignedToRole { get; private set; } // e.g. "Admin" for approval
    public Guid? ApprovedNextStepId { get; private set; }
    public Guid? RejectedNextStepId { get; private set; }
    public ApprovalPolicy ApprovalPolicy { get; private set; } = ApprovalPolicy.AnyOneApprove;
    public string? ApproversJson { get; private set; } // JSON array of user GUIDs, e.g. ["guid1","guid2"]
    public string? ActivityId { get; private set; } // Legacy activity identifier (maps to transaction.ActivityId)
    public string? StageType { get; private set; } // Legacy stage type (maps to transaction.StageType)
    /// <summary>JSON array of outgoing rules: [{ Id, ProceedAction, ToBlockId }] from designer Rules.</summary>
    public string? ActionsJson { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private WorkflowStep() { } // EF

    /// <summary>Create a workflow step.</summary>
    public static WorkflowStep Create(Guid workflowId, string name, StepType stepType, int order, string? description = null, string? config = null, bool isRequired = true, Guid? assignedToUserId = null, string? assignedToRole = null, Guid? approvedNextStepId = null, Guid? rejectedNextStepId = null, ApprovalPolicy approvalPolicy = ApprovalPolicy.AnyOneApprove, string? approversJson = null, string? activityId = null, string? stageType = null, string? actionsJson = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Step name is required.", nameof(name));

        return new WorkflowStep
        {
            Id = Guid.NewGuid(),
            WorkflowId = workflowId,
            Name = name.Trim(),
            Description = description?.Trim(),
            StepType = stepType,
            Order = order,
            Config = config?.Trim(),
            IsRequired = isRequired,
            AssignedToUserId = assignedToUserId,
            AssignedToRole = assignedToRole?.Trim(),
            ApprovedNextStepId = approvedNextStepId,
            RejectedNextStepId = rejectedNextStepId,
            ApprovalPolicy = approvalPolicy,
            ApproversJson = approversJson?.Trim(),
            ActivityId = activityId?.Trim(),
            StageType = stageType?.Trim(),
            ActionsJson = actionsJson?.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>Update step definition.</summary>
    public void Update(string? name, string? description, StepType? stepType, int? order, string? config, bool? isRequired, Guid? assignedToUserId, string? assignedToRole, Guid? approvedNextStepId = null, Guid? rejectedNextStepId = null, ApprovalPolicy? approvalPolicy = null, string? approversJson = null, string? activityId = null, string? stageType = null, string? actionsJson = null)
    {
        if (!string.IsNullOrWhiteSpace(name))
            Name = name.Trim();
        if (description != null)
            Description = description.Trim();
        if (stepType.HasValue)
            StepType = stepType.Value;
        if (order.HasValue)
            Order = order.Value;
        if (config != null)
            Config = config.Trim();
        if (isRequired.HasValue)
            IsRequired = isRequired.Value;
        if (assignedToUserId.HasValue)
            AssignedToUserId = assignedToUserId;
        if (assignedToRole != null)
            AssignedToRole = assignedToRole.Trim();
        if (approvedNextStepId.HasValue)
            ApprovedNextStepId = approvedNextStepId;
        if (rejectedNextStepId.HasValue)
            RejectedNextStepId = rejectedNextStepId;
        if (approvalPolicy.HasValue)
            ApprovalPolicy = approvalPolicy.Value;
        if (approversJson != null)
            ApproversJson = approversJson.Trim();
        if (activityId != null)
            ActivityId = activityId.Trim();
        if (stageType != null)
            StageType = stageType.Trim();
        if (actionsJson != null)
            ActionsJson = actionsJson.Trim();
    }

    /// <summary>Get approver user IDs from ApproversJson. Returns empty if null/empty. Falls back to AssignedToUserId if single approver.</summary>
    public IReadOnlyList<Guid> GetApproverIds()
    {
        if (!string.IsNullOrWhiteSpace(ApproversJson))
        {
            try
            {
                var strings = System.Text.Json.JsonSerializer.Deserialize<string[]>(ApproversJson);
                if (strings != null && strings.Length > 0)
                {
                    var list = new List<Guid>();
                    foreach (var s in strings)
                    {
                        if (Guid.TryParse(s, out var g))
                            list.Add(g);
                    }
                    if (list.Count > 0)
                        return list;
                }
            }
            catch { /* invalid JSON */ }
        }
        if (AssignedToUserId.HasValue)
            return new[] { AssignedToUserId.Value };
        return Array.Empty<Guid>();
    }
}
