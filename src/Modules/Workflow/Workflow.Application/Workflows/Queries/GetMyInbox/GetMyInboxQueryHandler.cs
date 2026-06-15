using MediatR;
using SaaSApp.Workflow.Application.Contracts;

namespace SaaSApp.Workflow.Application.Workflows.Queries.GetMyInbox;

public sealed class GetMyInboxQueryHandler : IRequestHandler<GetMyInboxQuery, GetMyInboxQueryResult>
{
    private readonly IWorkflowRepository _repository;
    private readonly ICurrentUserProvider _currentUserProvider;

    public GetMyInboxQueryHandler(IWorkflowRepository repository, ICurrentUserProvider currentUserProvider)
    {
        _repository = repository;
        _currentUserProvider = currentUserProvider;
    }

    public async Task<GetMyInboxQueryResult> Handle(GetMyInboxQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserProvider.GetUserId() ?? throw new InvalidOperationException("User context is required.");

        var (items, totalCount) = await _repository.GetMyInboxAsync(userId, request.PageNumber, request.PageSize, request.WorkflowId, cancellationToken);

        var inboxItems = items.Select(i => new InboxWorkflowItem(
            i.Id,
            i.WorkflowId,
            i.WorkflowName,
            i.ReferenceNumber,
            i.CustomerName,
            i.Status,
            i.Priority,
            i.CreatedAtUtc,
            i.LastActivityAtUtc,
            i.Sla != null,
            i.Sla?.ResponseStatus.ToString()
        )).ToList();

        return new GetMyInboxQueryResult(inboxItems, totalCount, request.PageNumber, request.PageSize);
    }
}
