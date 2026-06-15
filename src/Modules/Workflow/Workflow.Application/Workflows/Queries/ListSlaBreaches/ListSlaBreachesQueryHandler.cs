using MediatR;
using SaaSApp.Workflow.Application.Contracts;

namespace SaaSApp.Workflow.Application.Workflows.Queries.ListSlaBreaches;

public sealed class ListSlaBreachesQueryHandler : IRequestHandler<ListSlaBreachesQuery, ListSlaBreachesQueryResult>
{
    private readonly IWorkflowRepository _repository;

    public ListSlaBreachesQueryHandler(IWorkflowRepository repository)
    {
        _repository = repository;
    }

    public async Task<ListSlaBreachesQueryResult> Handle(ListSlaBreachesQuery request, CancellationToken cancellationToken)
    {
        var breaches = await _repository.ListSlaBreachesAsync(cancellationToken);
        var items = breaches.Select(b => new SlaBreachItem(
            b.InstanceId,
            b.WorkflowId,
            b.WorkflowName,
            b.InstanceStatus,
            b.Priority,
            b.ResponseStatus,
            b.ResolutionStatus,
            b.ResponseDeadline,
            b.ResolutionDeadline,
            b.IsEscalated,
            b.CreatedAtUtc)).ToList();
        return new ListSlaBreachesQueryResult(items);
    }
}
