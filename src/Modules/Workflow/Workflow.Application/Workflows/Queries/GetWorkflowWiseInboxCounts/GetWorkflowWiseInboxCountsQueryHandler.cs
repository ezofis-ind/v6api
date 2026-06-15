using MediatR;
using SaaSApp.Workflow.Application.Contracts;

namespace SaaSApp.Workflow.Application.Workflows.Queries.GetWorkflowWiseInboxCounts;

public sealed class GetWorkflowWiseInboxCountsQueryHandler : IRequestHandler<GetWorkflowWiseInboxCountsQuery, GetWorkflowWiseInboxCountsQueryResult>
{
    private readonly IWorkflowRepository _repository;
    private readonly ICurrentUserProvider _currentUserProvider;

    public GetWorkflowWiseInboxCountsQueryHandler(IWorkflowRepository repository, ICurrentUserProvider currentUserProvider)
    {
        _repository = repository;
        _currentUserProvider = currentUserProvider;
    }

    public async Task<GetWorkflowWiseInboxCountsQueryResult> Handle(GetWorkflowWiseInboxCountsQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserProvider.GetUserId() ?? throw new InvalidOperationException("User context is required.");

        var counts = await _repository.GetWorkflowWiseInboxCountsAsync(userId, cancellationToken);

        var items = counts.Select(c => new WorkflowInboxCountItem(c.WorkflowId, c.WorkflowName, c.InboxCount)).ToList();
        return new GetWorkflowWiseInboxCountsQueryResult(items);
    }
}
