namespace SaaSApp.Catalog.Entities;

/// <summary>Tenant credit allocation for a month/year. Stored in catalog dbo.creditMaster.</summary>
public sealed class CreditMaster
{
    public int Id { get; set; }
    public Guid TenantId { get; set; }
    public int AllocationMonth { get; set; }
    public int AllocationYear { get; set; }
    public string? CreditType { get; set; }
    public int InitialCredit { get; set; }
    public int BalanceCredit { get; set; }
    public string? Remarks { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public int? ParentAllocationId { get; set; }
    public string? SubscriptionType { get; set; }
    public DateTime? ValidFromDate { get; set; }
    public DateTime? ValidToDate { get; set; }
    public bool? IsCarryForward { get; set; }
    public int? Priority { get; set; }
    public string? Status { get; set; }
    public int? CarryForwardCredit { get; set; }
    public int? ExtraConsumedCredit { get; set; }
    public int? TopUpBalanceCredit { get; set; }
    public int? OverallConsumedCredit { get; set; }
}
