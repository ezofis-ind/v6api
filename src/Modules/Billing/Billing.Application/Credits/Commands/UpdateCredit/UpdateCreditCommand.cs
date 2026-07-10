using MediatR;
using SaaSApp.Billing.Application.Contracts;

namespace SaaSApp.Billing.Application.Credits.Commands.UpdateCredit;

public sealed record UpdateCreditCommand(Guid TenantId, Guid? UserId, CreditUpdateRequest Request)
    : IRequest<CreditUpdateResult>;

public sealed class UpdateCreditCommandHandler : IRequestHandler<UpdateCreditCommand, CreditUpdateResult>
{
    private readonly ICreditService _creditService;

    public UpdateCreditCommandHandler(ICreditService creditService) => _creditService = creditService;

    public Task<CreditUpdateResult> Handle(UpdateCreditCommand command, CancellationToken cancellationToken) =>
        _creditService.UpdateCreditAsync(command.TenantId, command.UserId, command.Request, cancellationToken);
}
