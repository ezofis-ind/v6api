namespace SaaSApp.Workflow.Domain.Enums;

/// <summary>Workflow definition status.</summary>
public enum WorkflowStatus
{
    /// <summary>Draft, not yet published.</summary>
    Draft = 0,
    
    /// <summary>Active and can be started.</summary>
    Active = 1,
    
    /// <summary>Archived, cannot start new instances.</summary>
    Archived = 2
}
