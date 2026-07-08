using MediatR;
using SaaSApp.Billing.Application.Contracts;

namespace SaaSApp.Billing.Application.Credits.Queries.GetCreditUsageDashboard;

public sealed record GetCreditUsageDashboardQuery(Guid TenantId, CreditUsageReportRequest Request)
    : IRequest<CreditUsageDashboardResult>;

public sealed class GetCreditUsageDashboardQueryHandler
    : IRequestHandler<GetCreditUsageDashboardQuery, CreditUsageDashboardResult>
{
    private readonly ICreditService _creditService;

    public GetCreditUsageDashboardQueryHandler(ICreditService creditService) => _creditService = creditService;

    public Task<CreditUsageDashboardResult> Handle(
        GetCreditUsageDashboardQuery query,
        CancellationToken cancellationToken) =>
        _creditService.GetCreditUsageDashboardAsync(query.TenantId, query.Request, cancellationToken);
}
