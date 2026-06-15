using SaaSApp.SharedKernel.Domain;
using SaaSApp.SharedKernel.Domain.Interfaces;
using SaaSApp.Workflow.Domain.Enums;

namespace SaaSApp.Workflow.Domain.Entities;

/// <summary>Workflow instance (execution). Each tenant has their own instances.</summary>
public sealed class WorkflowInstance : Entity<Guid>, ITenantEntity
{
    public Guid TenantId { get; private set; }
    public Guid WorkflowId { get; private set; }
    public string WorkflowName { get; private set; } = null!; // Snapshot at start
    public int WorkflowVersion { get; private set; }
    public WorkflowInstanceStatus Status { get; private set; }
    public Guid? CurrentStepInstanceId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? StartedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public Guid StartedBy { get; private set; }
    public string? Context { get; private set; } // JSON: workflow variables, input data
    public string? ErrorMessage { get; private set; }

    // Extended fields from old schema
    public string? ReferenceNumber { get; private set; } // External reference/ticket number
    public string? CustomerName { get; private set; }
    public string? CustomerEmail { get; private set; }
    public string? CustomerPhone { get; private set; }
    public string? Department { get; private set; }
    public string? Category { get; private set; }
    public int Priority { get; private set; } // 0=Low, 1=Normal, 2=High, 3=Critical
    public string? Tags { get; private set; } // Comma-separated or JSON array
    public string? CustomFieldsJson { get; private set; } // Dynamic custom fields
    public Guid? AssignedToUserId { get; private set; }
    public Guid? AssignedToGroupId { get; private set; }
    public DateTime? LastActivityAtUtc { get; private set; }
    public int ViewCount { get; private set; }
    public bool IsArchived { get; private set; }
    public DateTime? ArchivedAtUtc { get; private set; }
    public string? SourceType { get; private set; } // Email, API, Manual, Portal, etc.
    public string? SourceId { get; private set; } // External source ID

    private readonly List<WorkflowStepInstance> _stepInstances = new();
    public IReadOnlyList<WorkflowStepInstance> StepInstances => _stepInstances.AsReadOnly();

    private readonly List<WorkflowComment> _comments = new();
    public IReadOnlyList<WorkflowComment> Comments => _comments.AsReadOnly();

    private readonly List<WorkflowAttachment> _attachments = new();
    public IReadOnlyList<WorkflowAttachment> Attachments => _attachments.AsReadOnly();

    public WorkflowInstanceSla? Sla { get; private set; }

    private WorkflowInstance() { } // EF

    /// <summary>Create a workflow instance.</summary>
    public static WorkflowInstance Create(Guid tenantId, Guid workflowId, string workflowName, int workflowVersion, Guid startedBy, string? context = null, string? referenceNumber = null, string? customerName = null, string? customerEmail = null, int priority = 1, string? sourceType = null)
    {
        return new WorkflowInstance
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WorkflowId = workflowId,
            WorkflowName = workflowName,
            WorkflowVersion = workflowVersion,
            Status = WorkflowInstanceStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow,
            StartedBy = startedBy,
            Context = context?.Trim(),
            ReferenceNumber = referenceNumber?.Trim(),
            CustomerName = customerName?.Trim(),
            CustomerEmail = customerEmail?.Trim(),
            Priority = priority,
            SourceType = sourceType?.Trim(),
            LastActivityAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>Start the workflow instance.</summary>
    public void Start()
    {
        if (Status != WorkflowInstanceStatus.Pending)
            throw new InvalidOperationException("Workflow already started.");
        Status = WorkflowInstanceStatus.Running;
        StartedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Pause the workflow instance.</summary>
    public void Pause(Guid userId)
    {
        if (Status != WorkflowInstanceStatus.Running)
            throw new InvalidOperationException("Only running workflows can be paused.");
        Status = WorkflowInstanceStatus.Paused;
        LastActivityAtUtc = DateTime.UtcNow;
    }

    /// <summary>Resume the workflow instance.</summary>
    public void Resume(Guid userId)
    {
        if (Status != WorkflowInstanceStatus.Paused)
            throw new InvalidOperationException("Only paused workflows can be resumed.");
        Status = WorkflowInstanceStatus.Running;
        LastActivityAtUtc = DateTime.UtcNow;
    }

    /// <summary>Complete the workflow instance.</summary>
    public void Complete(Guid userId)
    {
        Status = WorkflowInstanceStatus.Completed;
        CompletedAtUtc = DateTime.UtcNow;
        LastActivityAtUtc = DateTime.UtcNow;
    }

    /// <summary>Reopen a completed instance when legacy process is still running (FlowStatus = 0).</summary>
    public void Reopen(Guid userId)
    {
        if (Status == WorkflowInstanceStatus.Cancelled)
            throw new InvalidOperationException("Cannot reopen a cancelled workflow.");
        Status = WorkflowInstanceStatus.Running;
        CompletedAtUtc = null;
        LastActivityAtUtc = DateTime.UtcNow;
    }

    /// <summary>Fail the workflow instance.</summary>
    public void Fail(string errorMessage)
    {
        Status = WorkflowInstanceStatus.Failed;
        ErrorMessage = errorMessage?.Trim();
        CompletedAtUtc = DateTime.UtcNow;
        LastActivityAtUtc = DateTime.UtcNow;
    }

    /// <summary>Cancel the workflow instance.</summary>
    public void Cancel(Guid userId)
    {
        if (Status == WorkflowInstanceStatus.Completed)
            throw new InvalidOperationException("Cannot cancel completed workflow.");
        Status = WorkflowInstanceStatus.Cancelled;
        CompletedAtUtc = DateTime.UtcNow;
        LastActivityAtUtc = DateTime.UtcNow;
    }

    /// <summary>Reassign workflow to another user.</summary>
    public void Reassign(Guid newUserId)
    {
        AssignedToUserId = newUserId;
        LastActivityAtUtc = DateTime.UtcNow;
    }

    /// <summary>Set current step.</summary>
    public void SetCurrentStep(Guid stepInstanceId)
    {
        CurrentStepInstanceId = stepInstanceId;
    }

    /// <summary>Add a step instance.</summary>
    public void AddStepInstance(WorkflowStepInstance stepInstance)
    {
        if (stepInstance == null)
            throw new ArgumentNullException(nameof(stepInstance));
        _stepInstances.Add(stepInstance);
    }

    /// <summary>Set SLA tracking for this instance.</summary>
    public void SetSla(WorkflowInstanceSla sla)
    {
        if (sla == null)
            throw new ArgumentNullException(nameof(sla));
        Sla = sla;
    }

    /// <summary>Update instance metadata.</summary>
    public void UpdateMetadata(string? referenceNumber = null, string? customerName = null, string? customerEmail = null, string? customerPhone = null, string? department = null, string? category = null, int? priority = null, string? tags = null, Guid? assignedToUserId = null, Guid? assignedToGroupId = null)
    {
        if (referenceNumber != null) ReferenceNumber = referenceNumber.Trim();
        if (customerName != null) CustomerName = customerName.Trim();
        if (customerEmail != null) CustomerEmail = customerEmail.Trim();
        if (customerPhone != null) CustomerPhone = customerPhone.Trim();
        if (department != null) Department = department.Trim();
        if (category != null) Category = category.Trim();
        if (priority.HasValue) Priority = priority.Value;
        if (tags != null) Tags = tags.Trim();
        if (assignedToUserId.HasValue) AssignedToUserId = assignedToUserId;
        if (assignedToGroupId.HasValue) AssignedToGroupId = assignedToGroupId;
        LastActivityAtUtc = DateTime.UtcNow;
    }

    /// <summary>Update custom fields.</summary>
    public void UpdateCustomFields(string customFieldsJson)
    {
        CustomFieldsJson = customFieldsJson?.Trim();
        LastActivityAtUtc = DateTime.UtcNow;
    }

    /// <summary>Increment view count.</summary>
    public void IncrementViewCount()
    {
        ViewCount++;
        LastActivityAtUtc = DateTime.UtcNow;
    }

    /// <summary>Archive instance.</summary>
    public void Archive()
    {
        IsArchived = true;
        ArchivedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Unarchive instance.</summary>
    public void Unarchive()
    {
        IsArchived = false;
        ArchivedAtUtc = null;
    }

    /// <summary>Add a comment.</summary>
    public void AddComment(WorkflowComment comment)
    {
        if (comment == null)
            throw new ArgumentNullException(nameof(comment));
        _comments.Add(comment);
        LastActivityAtUtc = DateTime.UtcNow;
    }

    /// <summary>Add an attachment.</summary>
    public void AddAttachment(WorkflowAttachment attachment)
    {
        if (attachment == null)
            throw new ArgumentNullException(nameof(attachment));
        _attachments.Add(attachment);
        LastActivityAtUtc = DateTime.UtcNow;
    }
}
