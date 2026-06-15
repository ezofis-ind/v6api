namespace SaaSApp.Workflow.Domain.Enums;

/// <summary>SLA compliance status.</summary>
public enum SlaStatus
{
    /// <summary>Within SLA timeframe.</summary>
    OnTime = 0,
    
    /// <summary>Approaching SLA deadline (e.g., 80% of time elapsed).</summary>
    AtRisk = 1,
    
    /// <summary>SLA deadline breached.</summary>
    Breached = 2,
    
    /// <summary>Completed within SLA.</summary>
    Met = 3,
    
    /// <summary>Completed after SLA breach.</summary>
    Missed = 4
}
