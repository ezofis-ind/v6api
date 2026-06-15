using MediatR;
using SaaSApp.Workflow.Application.Contracts;

namespace SaaSApp.Workflow.Application.Workflows.Queries.GetWorkflowCounts;

public sealed class GetWorkflowCountsQueryHandler : IRequestHandler<GetWorkflowCountsQuery, GetWorkflowCountsQueryResult>
{
    private readonly IWorkflowRepository _repository;
    private readonly ICurrentUserProvider _currentUserProvider;

    public GetWorkflowCountsQueryHandler(IWorkflowRepository repository, ICurrentUserProvider currentUserProvider)
    {
        _repository = repository;
        _currentUserProvider = currentUserProvider;
    }

    public async Task<GetWorkflowCountsQueryResult> Handle(GetWorkflowCountsQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserProvider.GetUserId() ?? throw new InvalidOperationException("User context is required.");

        var counts = await _repository.GetWorkflowCountsAsync(userId, cancellationToken);

        return new GetWorkflowCountsQueryResult(
            counts.InboxCount,
            counts.SentCount,
            counts.CompletedCount,
            counts.TotalActive
        );
    }
}
