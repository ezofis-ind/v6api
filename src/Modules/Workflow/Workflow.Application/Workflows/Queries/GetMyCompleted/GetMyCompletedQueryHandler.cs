using MediatR;
using SaaSApp.Workflow.Application.Contracts;

namespace SaaSApp.Workflow.Application.Workflows.Queries.GetMyCompleted;

public sealed class GetMyCompletedQueryHandler : IRequestHandler<GetMyCompletedQuery, GetMyCompletedQueryResult>
{
    private readonly IWorkflowRepository _repository;
    private readonly ICurrentUserProvider _currentUserProvider;

    public GetMyCompletedQueryHandler(IWorkflowRepository repository, ICurrentUserProvider currentUserProvider)
    {
        _repository = repository;
        _currentUserProvider = currentUserProvider;
    }

    public async Task<GetMyCompletedQueryResult> Handle(GetMyCompletedQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserProvider.GetUserId() ?? throw new InvalidOperationException("User context is required.");

        var (items, totalCount) = await _repository.GetMyCompletedAsync(userId, request.PageNumber, request.PageSize, cancellationToken);

        var completedItems = items.Select(i => new CompletedWorkflowItem(
            i.Id,
            i.WorkflowId,
            i.WorkflowName,
            i.ReferenceNumber,
            i.CustomerName,
            i.Priority,
            i.CreatedAtUtc,
            i.CompletedAtUtc!.Value,
            i.CompletedAtUtc!.Value - i.CreatedAtUtc,
            i.StartedBy == userId,
            i.AssignedToUserId == userId
        )).ToList();

        return new GetMyCompletedQueryResult(completedItems, totalCount, request.PageNumber, request.PageSize);
    }
}
