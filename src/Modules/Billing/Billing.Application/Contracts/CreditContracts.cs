namespace SaaSApp.Billing.Application.Contracts;

public enum CreditUpdateStatus
{
    Failed = 0,
    Success = 1,
    LimitExceeded = 2
}

public enum CreditUsagePeriod
{
    Today,
    Yesterday,
    Monthly,
    Quarterly,
    Yearly
}

public sealed record CreditUpdateRequest(
    string ActivityType,
    string SubActivity,
    string Identify,
    int IdentifyId,
    string? Remarks,
    int Credit = 1,
    string? Env = null,
    int? InputTokens = null,
    int? OutputTokens = null,
    int? TotalTokens = null);

public sealed record CreditUpdateResult(CreditUpdateStatus Status, string Message);

public sealed record CreditMasterDto(
    int Id,
    Guid TenantId,
    int AllocationMonth,
    int AllocationYear,
    string? CreditType,
    int InitialCredit,
    int BalanceCredit,
    string? Remarks,
    string? Status,
    int? OverallConsumedCredit,
    DateTime? ValidFromDate,
    DateTime? ValidToDate);

public sealed record CreditTransactionItemDto(
    int Id,
    string? ActivityType,
    string? SubActivityType,
    string? IdentifyTable,
    int? IdentifyId,
    string? Remarks,
    int Credit,
    int? InputTokens,
    int? OutputTokens,
    int? TotalTokens,
    DateTime? CreatedAt);

public sealed record CreditUsageResult(
    CreditUsagePeriod Period,
    DateTime RangeStartUtc,
    DateTime RangeEndUtc,
    int TotalCreditsConsumed,
    int TransactionCount,
    IReadOnlyList<CreditTransactionItemDto> Transactions);

public sealed record CreditUsageReportRequest(
    string Period = "monthly",
    int? Year = null,
    int? Month = null);

public sealed record CreditUsageTypeSummaryDto(string Type, int CreditsUsed);

public sealed record CreditUsageTimelinePointDto(string Label, int CreditsUsed, DateTime? BucketStartUtc);

public sealed record CreditUsageMonthSummaryDto(int Year, int Month, string Label, int CreditsUsed);

/// <summary>Dashboard payload for credit usage widgets (summary, distribution, timeline, monthly list, pie chart).</summary>
public sealed record CreditUsageDashboardResult(
    CreditUsagePeriod Period,
    string PeriodLabel,
    DateTime RangeStartUtc,
    DateTime RangeEndUtc,
    int TotalCreditsConsumed,
    int TransactionCount,
    IReadOnlyList<CreditUsageTypeSummaryDto> HighestConsumption,
    IReadOnlyList<CreditUsageTypeSummaryDto> DistributionReport,
    /// <summary>Service-level split for the Overall Credit Split pie chart (AP Agent, OCR Agent, etc.).</summary>
    IReadOnlyList<CreditUsageTypeSummaryDto> OverallCreditSplit,
    IReadOnlyList<CreditUsageTimelinePointDto> Timeline,
    IReadOnlyList<CreditUsageMonthSummaryDto>? MonthlyConsumption,
    IReadOnlyList<CreditTransactionItemDto> Transactions);

public static class CreditUsagePeriodParser
{
    public static CreditUsagePeriod Parse(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "today" => CreditUsagePeriod.Today,
            "yesterday" => CreditUsagePeriod.Yesterday,
            "monthly" => CreditUsagePeriod.Monthly,
            "quarterly" => CreditUsagePeriod.Quarterly,
            "yearly" => CreditUsagePeriod.Yearly,
            _ => Enum.TryParse<CreditUsagePeriod>(value, true, out var parsed)
                ? parsed
                : CreditUsagePeriod.Monthly
        };
}
