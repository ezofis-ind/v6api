namespace SaaSApp.Workflow.Domain.Enums;

/// <summary>How a workflow can be triggered.</summary>
public enum TriggerType
{
    /// <summary>Started manually by user.</summary>
    Manual = 0,
    
    /// <summary>Scheduled (cron, recurring).</summary>
    Scheduled = 1,
    
    /// <summary>Triggered by external event (webhook, domain event).</summary>
    Event = 2,
    
    /// <summary>Triggered by API call.</summary>
    Api = 3
}
