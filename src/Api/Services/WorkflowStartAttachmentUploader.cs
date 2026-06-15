using SaaSApp.Repository.Application.Contracts;
using SaaSApp.Workflow.Application.Contracts;

namespace SaaSApp.Api.Services;

public sealed class WorkflowStartAttachmentUploader : IWorkflowStartAttachmentUploader
{
    private readonly IRepositoryFileUploadService _fileUpload;

    public WorkflowStartAttachmentUploader(IRepositoryFileUploadService fileUpload)
    {
        _fileUpload = fileUpload;
    }

    public async Task<WorkflowStartAttachmentUploadResult?> UploadAsync(
        Guid tenantId,
        Guid repositoryId,
        Guid workflowId,
        Guid instanceId,
        int? transactionId,
        Stream fileStream,
        string fileName,
        string? contentType,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var request = new RepositoryUploadItemRequest(
            fileStream,
            fileName,
            contentType,
            workflowId,
            ProcessId: null,
            instanceId,
            transactionId);

        var result = await _fileUpload.UploadItemAsync(
            repositoryId,
            tenantId,
            request,
            userId,
            cancellationToken);

        return new WorkflowStartAttachmentUploadResult(result.FilePath, result.ItemId);
    }
}
