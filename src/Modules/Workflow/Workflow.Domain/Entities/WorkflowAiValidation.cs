using SaaSApp.SharedKernel.Domain;
using SaaSApp.SharedKernel.Domain.Interfaces;

namespace SaaSApp.Workflow.Domain.Entities;

/// <summary>AI/ML validation result for a workflow instance. Tracks agent responses and OCR validation.</summary>
public sealed class WorkflowAiValidation : Entity<Guid>, ITenantEntity
{
    public Guid TenantId { get; private set; }
    public Guid WorkflowInstanceId { get; private set; }
    public Guid? StepInstanceId { get; private set; }
    public string Type { get; private set; } = null!; // Agent, OCR, ML, etc.
    public string? AgentResponse { get; private set; } // JSON: AI agent response
    public string? FieldName { get; private set; }
    public string? FormValue { get; private set; }
    public string? OcrValue { get; private set; }
    public string? ValidationStatus { get; private set; } // Match, Mismatch, Pending, etc.
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ModifiedAtUtc { get; private set; }
    public Guid CreatedBy { get; private set; }
    public Guid? ModifiedBy { get; private set; }
    public bool IsDeleted { get; private set; }

    private WorkflowAiValidation() { } // EF

    /// <summary>Create an AI validation record.</summary>
    public static WorkflowAiValidation Create(Guid tenantId, Guid workflowInstanceId, string type, Guid createdBy, Guid? stepInstanceId = null, string? agentResponse = null, string? fieldName = null, string? formValue = null, string? ocrValue = null, string? validationStatus = null)
    {
        if (string.IsNullOrWhiteSpace(type))
            throw new ArgumentException("Type is required.", nameof(type));

        return new WorkflowAiValidation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WorkflowInstanceId = workflowInstanceId,
            StepInstanceId = stepInstanceId,
            Type = type.Trim(),
            AgentResponse = agentResponse?.Trim(),
            FieldName = fieldName?.Trim(),
            FormValue = formValue?.Trim(),
            OcrValue = ocrValue?.Trim(),
            ValidationStatus = validationStatus?.Trim(),
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = createdBy
        };
    }

    /// <summary>Update validation result.</summary>
    public void Update(string? agentResponse, string? formValue, string? ocrValue, string? validationStatus, Guid modifiedBy)
    {
        if (agentResponse != null)
            AgentResponse = agentResponse.Trim();
        if (formValue != null)
            FormValue = formValue.Trim();
        if (ocrValue != null)
            OcrValue = ocrValue.Trim();
        if (validationStatus != null)
            ValidationStatus = validationStatus.Trim();
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
