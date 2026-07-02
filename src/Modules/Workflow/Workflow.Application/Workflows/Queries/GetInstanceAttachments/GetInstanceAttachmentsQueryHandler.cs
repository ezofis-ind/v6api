using MediatR;
using SaaSApp.Workflow.Application.Contracts;

namespace SaaSApp.Workflow.Application.Workflows.Queries.GetInstanceAttachments;

public sealed class GetInstanceAttachmentsQueryHandler : IRequestHandler<GetInstanceAttachmentsQuery, GetInstanceAttachmentsQueryResult>
{
    private readonly IWorkflowRepository _workflowRepository;
    private readonly IDynamicTableRepository _dynamicTableRepository;
    private readonly IWorkflowProcessAddonService _processAddon;

    public GetInstanceAttachmentsQueryHandler(
        IWorkflowRepository workflowRepository,
        IDynamicTableRepository dynamicTableRepository,
        IWorkflowProcessAddonService processAddon)
    {
        _workflowRepository = workflowRepository;
        _dynamicTableRepository = dynamicTableRepository;
        _processAddon = processAddon;
    }

    public async Task<GetInstanceAttachmentsQueryResult> Handle(GetInstanceAttachmentsQuery request, CancellationToken cancellationToken)
    {
        var instance = await _workflowRepository.GetInstanceByIdAsync(request.InstanceId, cancellationToken);
        WorkflowInstanceScopeValidator.EnsureInstanceBelongsToWorkflow(instance, request.WorkflowId, request.InstanceId);

        var workflow = await _workflowRepository.GetByIdAsync(request.WorkflowId, cancellationToken);
        var attachments = await _dynamicTableRepository.GetAttachmentsAsync(request.WorkflowId, request.InstanceId, cancellationToken);
        var workflowRepositoryGuid = TryParseRepositoryGuid(workflow?.RepositoryId);

        var attachmentItems = attachments.Select(a => new AttachmentItem(
            a.Id,
            a.WorkflowInstanceId,
            request.WorkflowId,
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

        var knownItemIds = attachmentItems
            .Where(a => a.ItemId is { } id && id != Guid.Empty)
            .Select(a => a.ItemId!.Value)
            .ToHashSet();

        var addons = await _processAddon.ListByProcessAsync(request.WorkflowId, request.InstanceId, cancellationToken);
        foreach (var addon in addons)
        {
            if (knownItemIds.Contains(addon.ItemId))
                continue;

            attachmentItems.Add(new AttachmentItem(
                Id: Guid.Empty,
                WorkflowInstanceId: request.InstanceId,
                WorkflowId: request.WorkflowId,
                FileName: addon.FileName ?? string.Empty,
                FilePath: string.Empty,
                FileSize: null,
                ContentType: null,
                CreatedAtUtc: addon.CreatedAt,
                CreatedBy: addon.CreatedBy,
                ModifiedBy: null,
                ModifiedAtUtc: null,
                RepositoryId: addon.RepositoryId,
                ItemId: addon.ItemId));

            knownItemIds.Add(addon.ItemId);
        }

        var tableName = _dynamicTableRepository.GetTableName(request.WorkflowId, "WorkflowAttachments");

        return new GetInstanceAttachmentsQueryResult(request.WorkflowId, request.InstanceId, attachmentItems, tableName);
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
