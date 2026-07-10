namespace SaaSApp.Api.Options;

/// <summary>
/// Default credit allocation seeded into catalog dbo.creditMaster when a tenant is created.
/// </summary>
public sealed class TenantDefaultCreditOptions
{
    public const string SectionName = "TenantDefaultCredit";

    /// <summary>When false, no credit master row is created on tenant signup.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Initial (and starting balance) credit granted for the signup month.</summary>
    public int InitialCredit { get; set; } = 1000;

    /// <summary>Optional credit type label (e.g. "Standard", "Trial").</summary>
    public string? CreditType { get; set; } = "Standard";

    /// <summary>Optional subscription type label.</summary>
    public string? SubscriptionType { get; set; } = "Trial";

    /// <summary>Optional status label for the allocation row.</summary>
    public string? Status { get; set; } = "Active";

    /// <summary>Remarks recorded on the seeded allocation row.</summary>
    public string? Remarks { get; set; } = "Default allocation on tenant signup";

    /// <summary>Number of days the allocation is valid from creation. 0 or less means no expiry set.</summary>
    public int ValidDays { get; set; } = 0;
}
