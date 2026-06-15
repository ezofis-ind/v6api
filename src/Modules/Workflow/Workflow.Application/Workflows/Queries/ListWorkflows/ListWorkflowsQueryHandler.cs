using MediatR;
using SaaSApp.Workflow.Application.Contracts;

namespace SaaSApp.Workflow.Application.Workflows.Queries.ListWorkflows;

public sealed class ListWorkflowsQueryHandler : IRequestHandler<ListWorkflowsQuery, ListWorkflowsQueryResult>
{
    private readonly IWorkflowRepository _repository;
    private readonly IUserEmailLookup _userEmails;

    public ListWorkflowsQueryHandler(IWorkflowRepository repository, IUserEmailLookup userEmails)
    {
        _repository = repository;
        _userEmails = userEmails;
    }

    public async Task<ListWorkflowsQueryResult> Handle(ListWorkflowsQuery request, CancellationToken cancellationToken)
    {
        var workflows = await _repository.ListAsync(cancellationToken);
        var userIds = workflows
            .Select(w => w.CreatedBy)
            .Concat(workflows.Where(w => w.ModifiedBy.HasValue).Select(w => w.ModifiedBy!.Value));
        var emails = await _userEmails.GetEmailsAsync(userIds, cancellationToken);

        var items = workflows.Select(w =>
        {
            emails.TryGetValue(w.CreatedBy, out var createdEmail);
            var modifiedEmail = createdEmail;
            if (w.ModifiedBy is Guid modifiedId && emails.TryGetValue(modifiedId, out var modEmail))
                modifiedEmail = modEmail;

            return new ListWorkflowsItem(
                w.Id,
                w.Name,
                w.Description,
                w.Status,
                w.TriggerType,
                w.Version,
                w.CreatedAtUtc,
                w.CreatedBy,
                w.ModifiedBy,
                w.ModifiedAtUtc,
                createdEmail,
                modifiedEmail);
        }).ToList();

        return new ListWorkflowsQueryResult(items);
    }
}
