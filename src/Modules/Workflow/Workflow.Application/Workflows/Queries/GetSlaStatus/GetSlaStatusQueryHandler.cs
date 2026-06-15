using MediatR;
using SaaSApp.Workflow.Application.Contracts;

namespace SaaSApp.Workflow.Application.Workflows.Queries.GetSlaStatus;

public sealed class GetSlaStatusQueryHandler : IRequestHandler<GetSlaStatusQuery, GetSlaStatusQueryResult?>
{
    private readonly IWorkflowRepository _repository;

    public GetSlaStatusQueryHandler(IWorkflowRepository repository)
    {
        _repository = repository;
    }

    public async Task<GetSlaStatusQueryResult?> Handle(GetSlaStatusQuery request, CancellationToken cancellationToken)
    {
        var instance = await _repository.GetInstanceByIdAsync(request.InstanceId, cancellationToken);
        if (instance?.Sla == null)
            return null;

        var sla = instance.Sla;
        sla.UpdateStatus(); // Refresh status based on current time

        return new GetSlaStatusQueryResult(
            instance.Id,
            sla.Priority,
            sla.ResponseDeadline,
            sla.ResolutionDeadline,
            sla.EscalationDeadline,
            sla.ResponseAchievedAt,
            sla.ResolutionAchievedAt,
            sla.ResponseStatus,
            sla.ResolutionStatus,
            sla.IsEscalated,
            sla.GetResponseTimeRemaining(),
            sla.GetResolutionTimeRemaining());
    }
}
