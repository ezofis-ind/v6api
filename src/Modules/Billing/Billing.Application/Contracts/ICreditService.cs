namespace SaaSApp.Billing.Application.Contracts;

public interface ICreditService
{
    Task<CreditUpdateResult> UpdateCreditAsync(
        Guid tenantId,
        Guid? userId,
        CreditUpdateRequest request,
        CancellationToken cancellationToken = default);

    Task<CreditMasterDto?> GetCreditMasterAsync(
        Guid tenantId,
        int? allocationMonth = null,
        int? allocationYear = null,
        string? creditType = null,
        CancellationToken cancellationToken = default);

    Task<CreditUsageResult> GetCreditUsageAsync(
        Guid tenantId,
        CreditUsagePeriod period,
        CancellationToken cancellationToken = default);

    Task<CreditUsageDashboardResult> GetCreditUsageDashboardAsync(
        Guid tenantId,
        CreditUsageReportRequest request,
        CancellationToken cancellationToken = default);
}
