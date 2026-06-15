using MediatR;
using SaaSApp.Workflow.Application.Contracts;

namespace SaaSApp.Workflow.Application.Workflows.Queries.GetMySent;

public sealed class GetMySentQueryHandler : IRequestHandler<GetMySentQuery, GetMySentQueryResult>
{
    private readonly IWorkflowRepository _repository;
    private readonly ICurrentUserProvider _currentUserProvider;

    public GetMySentQueryHandler(IWorkflowRepository repository, ICurrentUserProvider currentUserProvider)
    {
        _repository = repository;
        _currentUserProvider = currentUserProvider;
    }

    public async Task<GetMySentQueryResult> Handle(GetMySentQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserProvider.GetUserId() ?? throw new InvalidOperationException("User context is required.");

        var (items, totalCount) = await _repository.GetMySentAsync(userId, request.PageNumber, request.PageSize, cancellationToken);

        var sentItems = items.Select(i => new SentWorkflowItem(
            i.Id,
            i.WorkflowId,
            i.WorkflowName,
            i.ReferenceNumber,
            i.CustomerName,
            i.Status,
            i.Priority,
            i.CreatedAtUtc,
            i.CompletedAtUtc,
            i.AssignedToUserId
        )).ToList();

        return new GetMySentQueryResult(sentItems, totalCount, request.PageNumber, request.PageSize);
    }
}
