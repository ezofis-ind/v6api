using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using SaaSApp.Workflow.Application.Contracts;
using System.IO;

namespace SaaSApp.Workflow.Infrastructure.Services;

/// <summary>Stores workflow JSON definitions in blob storage or file system.</summary>
public sealed class WorkflowJsonStorageService : IWorkflowJsonStorageService
{
    private readonly ITenantContext _tenantContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<WorkflowJsonStorageService> _logger;

    public WorkflowJsonStorageService(ITenantContext tenantContext, IConfiguration configuration, ILogger<WorkflowJsonStorageService> logger)
    {
        _tenantContext = tenantContext;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SaveWorkflowJsonAsync(Guid workflowId, string json, CancellationToken cancellationToken = default)
    {
        if (await TrySaveToBlobAsync(workflowId, json, cancellationToken))
            return;

        // Fallback for local/dev when blob config is not provided.
        var basePath = Path.Combine(AppContext.BaseDirectory, "Flow Json");
        var tenantFolder = (_tenantContext.TenantId?.ToString("N") ?? "default");
        var folderPath = Path.Combine(basePath, tenantFolder);
        Directory.CreateDirectory(folderPath);

        var filePath = Path.Combine(folderPath, $"{workflowId:N}.json");
        await File.WriteAllTextAsync(filePath, json, cancellationToken);

        _logger.LogInformation("Workflow JSON saved for workflow {WorkflowId} at {FilePath}", workflowId, filePath);
    }

    public async Task<string?> GetWorkflowJsonAsync(Guid workflowId, CancellationToken cancellationToken = default)
    {
        var fromBlob = await TryGetFromBlobAsync(workflowId, cancellationToken);
        if (fromBlob != null)
            return fromBlob;

        // Fallback for local/dev when blob config is not provided.
        var basePath = Path.Combine(AppContext.BaseDirectory, "Flow Json");
        var tenantFolder = (_tenantContext.TenantId?.ToString("N") ?? "default");
        var filePath = Path.Combine(basePath, tenantFolder, $"{workflowId:N}.json");
        if (!File.Exists(filePath))
            return null;

        _logger.LogInformation("Retrieving workflow JSON for workflow {WorkflowId} from {FilePath}", workflowId, filePath);
        return await File.ReadAllTextAsync(filePath, cancellationToken);
    }

    public async Task DeleteWorkflowJsonAsync(Guid workflowId, CancellationToken cancellationToken = default)
    {
        if (await TryDeleteFromBlobAsync(workflowId, cancellationToken))
            return;

        // Fallback for local/dev when blob config is not provided.
        var basePath = Path.Combine(AppContext.BaseDirectory, "Flow Json");
        var tenantFolder = (_tenantContext.TenantId?.ToString("N") ?? "default");
        var filePath = Path.Combine(basePath, tenantFolder, $"{workflowId:N}.json");
        if (!File.Exists(filePath))
            return;

        File.Delete(filePath);
        _logger.LogInformation("Deleted workflow JSON for workflow {WorkflowId} at {FilePath}", workflowId, filePath);
        await Task.CompletedTask;
    }

    private async Task<bool> TrySaveToBlobAsync(Guid workflowId, string json, CancellationToken cancellationToken)
    {
        var blobClient = GetBlobClient(workflowId);
        if (blobClient == null)
            return false;

        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        await blobClient.UploadAsync(stream, overwrite: true, cancellationToken);
        _logger.LogInformation("Workflow JSON saved to blob for workflow {WorkflowId}: {BlobPath}", workflowId, blobClient.Uri.ToString());
        return true;
    }

    private async Task<string?> TryGetFromBlobAsync(Guid workflowId, CancellationToken cancellationToken)
    {
        var blobClient = GetBlobClient(workflowId);
        if (blobClient == null)
            return null;

        if (!await blobClient.ExistsAsync(cancellationToken))
            return null;

        var download = await blobClient.DownloadContentAsync(cancellationToken);
        _logger.LogInformation("Workflow JSON loaded from blob for workflow {WorkflowId}: {BlobPath}", workflowId, blobClient.Uri.ToString());
        return download.Value.Content.ToString();
    }

    private async Task<bool> TryDeleteFromBlobAsync(Guid workflowId, CancellationToken cancellationToken)
    {
        var blobClient = GetBlobClient(workflowId);
        if (blobClient == null)
            return false;

        await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
        _logger.LogInformation("Workflow JSON deleted from blob for workflow {WorkflowId}: {BlobPath}", workflowId, blobClient.Uri.ToString());
        return true;
    }

    private BlobClient? GetBlobClient(Guid workflowId)
    {
        var connectionString = _configuration["WorkflowJsonStorage:Blob:ConnectionString"]
            ?? _configuration["WorkflowJsonStorage:ConnectionString"];
        if (string.IsNullOrWhiteSpace(connectionString))
            return null;

        var tenantId = _tenantContext.TenantId?.ToString("N").ToLowerInvariant() ?? "default";
        var containerPrefix = (_configuration["WorkflowJsonStorage:Blob:ContainerPrefix"] ?? "ezts").ToLowerInvariant();
        var containerName = $"{containerPrefix}{tenantId}";
        var blobPath = $"Flow Json/{workflowId:N}.json";

        var service = new BlobServiceClient(connectionString);
        var container = service.GetBlobContainerClient(containerName);
        container.CreateIfNotExists();
        return container.GetBlobClient(blobPath);
    }
}

