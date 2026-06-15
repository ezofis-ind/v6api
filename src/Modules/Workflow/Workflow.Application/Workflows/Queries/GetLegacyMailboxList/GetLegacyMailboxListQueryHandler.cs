using MediatR;
using SaaSApp.Workflow.Application.Contracts;

namespace SaaSApp.Workflow.Application.Workflows.Queries.GetLegacyMailboxList;

public sealed class GetLegacyMailboxListQueryHandler : IRequestHandler<GetLegacyMailboxListQuery, LegacyMailboxListResult>
{
    private readonly IWorkflowLegacyMailboxQueryService _mailboxQuery;
    private readonly ICurrentUserProvider _currentUserProvider;

    public GetLegacyMailboxListQueryHandler(
        IWorkflowLegacyMailboxQueryService mailboxQuery,
        ICurrentUserProvider currentUserProvider)
    {
        _mailboxQuery = mailboxQuery;
        _currentUserProvider = currentUserProvider;
    }

    public Task<LegacyMailboxListResult> Handle(GetLegacyMailboxListQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserProvider.GetUserId()
            ?? throw new InvalidOperationException("User context is required.");

        return _mailboxQuery.ListAsync(
            new LegacyMailboxListRequest(
                request.Kind,
                request.WorkflowId,
                request.InstanceId,
                request.TransactionId,
                userId,
                request.PageNumber,
                request.PageSize,
                request.SkipTotal),
            cancellationToken);
    }
}
