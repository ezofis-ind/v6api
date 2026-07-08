using MediatR;
using SaaSApp.Billing.Application.Contracts;

namespace SaaSApp.Billing.Application.Credits.Queries.GetCreditMaster;

public sealed record GetCreditMasterQuery(
    Guid TenantId,
    int? AllocationMonth = null,
    int? AllocationYear = null,
    string? CreditType = null) : IRequest<CreditMasterDto?>;

public sealed class GetCreditMasterQueryHandler : IRequestHandler<GetCreditMasterQuery, CreditMasterDto?>
{
    private readonly ICreditService _creditService;

    public GetCreditMasterQueryHandler(ICreditService creditService) => _creditService = creditService;

    public Task<CreditMasterDto?> Handle(GetCreditMasterQuery query, CancellationToken cancellationToken) =>
        _creditService.GetCreditMasterAsync(
            query.TenantId,
            query.AllocationMonth,
            query.AllocationYear,
            query.CreditType,
            cancellationToken);
}
