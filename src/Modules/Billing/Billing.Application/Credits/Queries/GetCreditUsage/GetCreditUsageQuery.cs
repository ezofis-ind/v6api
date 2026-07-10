using MediatR;
using SaaSApp.Billing.Application.Contracts;

namespace SaaSApp.Billing.Application.Credits.Queries.GetCreditUsage;

public sealed record GetCreditUsageQuery(Guid TenantId, CreditUsagePeriod Period)
    : IRequest<CreditUsageResult>;

public sealed class GetCreditUsageQueryHandler : IRequestHandler<GetCreditUsageQuery, CreditUsageResult>
{
    private readonly ICreditService _creditService;

    public GetCreditUsageQueryHandler(ICreditService creditService) => _creditService = creditService;

    public Task<CreditUsageResult> Handle(GetCreditUsageQuery query, CancellationToken cancellationToken) =>
        _creditService.GetCreditUsageAsync(query.TenantId, query.Period, cancellationToken);
}
