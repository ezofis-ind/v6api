using MediatR;
using SaaSApp.Workflow.Application.Contracts;

namespace SaaSApp.Workflow.Application.Workflows.Queries.GetLegacyMailboxInstanceCount;

public sealed class GetLegacyMailboxInstanceCountQueryHandler
    : IRequestHandler<GetLegacyMailboxInstanceCountQuery, LegacyMailboxInstanceCountResult>
{
    private readonly IWorkflowLegacyMailboxQueryService _mailboxQuery;
    private readonly ICurrentUserProvider _currentUserProvider;

    public GetLegacyMailboxInstanceCountQueryHandler(
        IWorkflowLegacyMailboxQueryService mailboxQuery,
        ICurrentUserProvider currentUserProvider)
    {
        _mailboxQuery = mailboxQuery;
        _currentUserProvider = currentUserProvider;
    }

    public Task<LegacyMailboxInstanceCountResult> Handle(
        GetLegacyMailboxInstanceCountQuery request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUserProvider.GetUserId()
            ?? throw new InvalidOperationException("User context is required.");

        return _mailboxQuery.GetInstanceCountsAsync(
            new LegacyMailboxInstanceCountRequest(request.WorkflowId, userId),
            cancellationToken);
    }
}
