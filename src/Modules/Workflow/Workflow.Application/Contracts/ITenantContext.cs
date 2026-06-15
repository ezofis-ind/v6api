namespace SaaSApp.Workflow.Application.Contracts;

/// <summary>Current tenant context for workflow operations.</summary>
public interface ITenantContext
{
    /// <summary>Current tenant ID from JWT or header.</summary>
    Guid? TenantId { get; }
    
    /// <summary>Current tenant connection string.</summary>
    string? ConnectionString { get; }
}
