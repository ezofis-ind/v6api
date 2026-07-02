using Microsoft.Data.SqlClient;
using SaaSApp.MultiTenancy;
using SaaSApp.Repository.Application.Contracts;
using SaaSApp.Repository.Infrastructure.Services;
using SaaSApp.Workflow.Application.Contracts;

namespace SaaSApp.Api.Services;

public sealed class WorkflowAttachmentArchiveService : IWorkflowAttachmentArchiveService
{
    private readonly IRepositoryArchiveFileUploadService _archiveUpload;
    private readonly IWorkflowProcessAddonService _processAddon;
    private readonly RepositoryWorkflowAttachService _workflowAttach;
    private readonly ITenantConnectionProvider _connectionProvider;

    public WorkflowAttachmentArchiveService(
        IRepositoryArchiveFileUploadService archiveUpload,
        IWorkflowProcessAddonService processAddon,
        RepositoryWorkflowAttachService workflowAttach,
        ITenantConnectionProvider connectionProvider)
    {
        _archiveUpload = archiveUpload;
        _processAddon = processAddon;
        _workflowAttach = workflowAttach;
        _connectionProvider = connectionProvider;
    }

    public async Task<WorkflowAttachmentArchiveResult> UploadAsync(
        Guid tenantId,
        Guid workflowId,
        Guid instanceId,
        Guid repositoryId,
        Stream fileStream,
        string fileName,
        string? contentType,
        long? fileSize,
        string? metadataJson,
        int? transactionId,
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
            transactionId,
            StorageProviderCode: null,
            fileSize,
            metadataJson);

        var upload = await _archiveUpload.UploadItemAsync(
            repositoryId,
            tenantId,
            request,
            userId,
            cancellationToken);

        var attachmentId = upload.WorkflowAttached
            ? await FindAttachmentIdAsync(workflowId, instanceId, upload.ItemId, cancellationToken)
            : await _workflowAttach.AttachAsync(
                workflowId,
                instanceId,
                transactionId,
                repositoryId,
                upload.ItemId,
                upload.FileName,
                upload.FilePath,
                fileSize,
                contentType,
                userId,
                cancellationToken: cancellationToken);

        var processAddonId = await _processAddon.InsertAsync(
            workflowId,
            instanceId,
            repositoryId,
            upload.ItemId,
            upload.FileName,
            transactionId,
            userId,
            cancellationToken);

        return new WorkflowAttachmentArchiveResult(
            attachmentId,
            upload.ItemId,
            repositoryId,
            workflowId,
            instanceId,
            processAddonId,
            upload.FileName,
            upload.FilePath,
            upload.StorageProviderCode,
            upload.FileVersion,
            upload.FolderId,
            upload.FolderPathSegments);
    }

    private async Task<Guid> FindAttachmentIdAsync(
        Guid workflowId,
        Guid instanceId,
        Guid itemId,
        CancellationToken cancellationToken)
    {
        var connectionString = _connectionProvider.ConnectionString
            ?? throw new InvalidOperationException("Tenant connection string not resolved.");
        var suffix = workflowId.ToString("N")[..8];
        var table = $"workflow.[WorkflowAttachments_{suffix}]";

        var sql = $"""
            SELECT TOP 1 Id
            FROM {table}
            WHERE WorkflowInstanceId = @InstanceId AND ItemId = @ItemId AND IsDeleted = 0
            ORDER BY CreatedAtUtc DESC;
            """;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@InstanceId", instanceId);
        cmd.Parameters.AddWithValue("@ItemId", itemId);
        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result is Guid guid ? guid : Guid.Empty;
    }
}
