using MediatR;
using SaaSApp.Workflow.Application.Contracts;

namespace SaaSApp.Workflow.Application.Workflows.Queries.GetInstanceAttachments;

public sealed class GetInstanceAttachmentsQueryHandler : IRequestHandler<GetInstanceAttachmentsQuery, GetInstanceAttachmentsQueryResult>
{
    private readonly IWorkflowRepository _workflowRepository;
    private readonly IDynamicTableRepository _dynamicTableRepository;

    public GetInstanceAttachmentsQueryHandler(
        IWorkflowRepository workflowRepository,
        IDynamicTableRepository dynamicTableRepository)
    {
        _workflowRepository = workflowRepository;
        _dynamicTableRepository = dynamicTableRepository;
    }

    public async Task<GetInstanceAttachmentsQueryResult> Handle(GetInstanceAttachmentsQuery request, CancellationToken cancellationToken)
    {
        // Get workflow instance to find workflow ID
        var instance = await _workflowRepository.GetInstanceByIdAsync(request.InstanceId, cancellationToken);
        if (instance == null)
            throw new InvalidOperationException("Workflow instance not found.");

        var workflow = await _workflowRepository.GetByIdAsync(instance.WorkflowId, cancellationToken);
        var attachments = await _dynamicTableRepository.GetAttachmentsAsync(instance.WorkflowId, request.InstanceId, cancellationToken);
        var workflowRepositoryGuid = TryParseRepositoryGuid(workflow?.RepositoryId);

        var attachmentItems = attachments.Select(a => new AttachmentItem(
            a.Id,
            a.WorkflowInstanceId,
            instance.WorkflowId,
            a.FileName ?? string.Empty,
            a.FilePath ?? string.Empty,
            a.FileSize,
            a.ContentType,
            a.CreatedAtUtc,
            a.CreatedBy,
            a.ModifiedBy,
            a.ModifiedAtUtc,
            RepositoryId: a.RepositoryId ?? workflowRepositoryGuid,
            ItemId: a.ItemId)).ToList();

        var tableName = _dynamicTableRepository.GetTableName(instance.WorkflowId, "WorkflowAttachments");

        return new GetInstanceAttachmentsQueryResult(attachmentItems, tableName);
    }

    private static Guid? TryParseRepositoryGuid(string? repositoryIdLink)
    {
        if (string.IsNullOrWhiteSpace(repositoryIdLink))
            return null;

        var trimmed = repositoryIdLink.Trim();
        if (Guid.TryParse(trimmed, out var guid))
            return guid;

        return trimmed.Length == 32 && Guid.TryParseExact(trimmed, "N", out guid) ? guid : null;
    }

}
