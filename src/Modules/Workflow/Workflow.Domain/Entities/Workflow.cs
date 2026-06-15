using SaaSApp.SharedKernel.Domain;
using SaaSApp.SharedKernel.Domain.Interfaces;
using SaaSApp.Workflow.Domain.Enums;

namespace SaaSApp.Workflow.Domain.Entities;

/// <summary>Workflow definition. Each tenant has their own workflows.</summary>
public sealed class Workflow : Entity<Guid>, ITenantEntity
{
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public WorkflowStatus Status { get; private set; }
    public TriggerType TriggerType { get; private set; }
    public string? TriggerConfig { get; private set; } // JSON: cron schedule, event name, etc.
    public int Version { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ModifiedAtUtc { get; private set; }
    public Guid CreatedBy { get; private set; }
    public Guid? ModifiedBy { get; private set; }
    public bool IsDeleted { get; private set; }

    /// <summary>Linked repository from Settings.General.InitiateUsing (GUID string or legacy numeric id).</summary>
    public string? RepositoryId { get; private set; }

    /// <summary>Linked form id from Settings.General.InitiateUsing (GUID string or legacy numeric id).</summary>
    public string? FormId { get; private set; }

    private readonly List<WorkflowStep> _steps = new();
    public IReadOnlyList<WorkflowStep> Steps => _steps.AsReadOnly();

    public WorkflowSla? Sla { get; private set; }

    private Workflow() { } // EF

    /// <summary>Create a new workflow definition.</summary>
    public static Workflow Create(Guid tenantId, string name, string? description, TriggerType triggerType, Guid createdBy, string? triggerConfig = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Workflow name is required.", nameof(name));

        return new Workflow
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name.Trim(),
            Description = description?.Trim(),
            Status = WorkflowStatus.Draft,
            TriggerType = triggerType,
            TriggerConfig = triggerConfig?.Trim(),
            Version = 1,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = createdBy
        };
    }

    /// <summary>Update workflow definition.</summary>
    public void Update(string? name, string? description, TriggerType? triggerType, string? triggerConfig, Guid modifiedBy)
    {
        if (!string.IsNullOrWhiteSpace(name))
            Name = name.Trim();
        if (description != null)
            Description = description.Trim();
        if (triggerType.HasValue)
            TriggerType = triggerType.Value;
        if (triggerConfig != null)
            TriggerConfig = triggerConfig.Trim();
        ModifiedAtUtc = DateTime.UtcNow;
        ModifiedBy = modifiedBy;
    }

    /// <summary>Persist repository/form links from designer InitiateUsing.</summary>
    public void SetInitiateLinks(string? repositoryId, string? formId, Guid modifiedBy)
    {
        RepositoryId = string.IsNullOrWhiteSpace(repositoryId) ? null : repositoryId.Trim();
        FormId = string.IsNullOrWhiteSpace(formId) ? null : formId.Trim();
        ModifiedAtUtc = DateTime.UtcNow;
        ModifiedBy = modifiedBy;
    }

    /// <summary>Publish workflow (make it active).</summary>
    public void Publish()
    {
        // Allow publishing without steps for simple/demo workflows; steps can be added later
        Status = WorkflowStatus.Active;
        ModifiedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Return a published workflow to draft (e.g. after designer save with DRAFT publish option).</summary>
    public void RevertToDraft(Guid modifiedBy)
    {
        Status = WorkflowStatus.Draft;
        ModifiedAtUtc = DateTime.UtcNow;
        ModifiedBy = modifiedBy;
    }

    /// <summary>Archive workflow (cannot start new instances).</summary>
    public void Archive()
    {
        Status = WorkflowStatus.Archived;
        ModifiedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Soft delete.</summary>
    public void SoftDelete()
    {
        IsDeleted = true;
        ModifiedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Add a step to the workflow.</summary>
    public void AddStep(WorkflowStep step)
    {
        if (step == null)
            throw new ArgumentNullException(nameof(step));
        _steps.Add(step);
    }

    /// <summary>Set SLA policy for this workflow.</summary>
    public void SetSla(WorkflowSla sla)
    {
        if (sla == null)
            throw new ArgumentNullException(nameof(sla));
        Sla = sla;
    }
}
