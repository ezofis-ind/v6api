using System.Globalization;
using System.Text.Json;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SaaSApp.MultiTenancy;
using SaaSApp.Repository.Application.Contracts;
using SaaSApp.Workflow.Application.Contracts;
using SaaSApp.Workflow.Application.Workflows.Commands.StartWorkflow;
using WorkflowEntity = SaaSApp.Workflow.Domain.Entities.Workflow;

namespace SaaSApp.Workflow.Infrastructure.Services;

public sealed class EmailIngestNormalWorkflowStarter : IEmailIngestNormalWorkflowStarter
{
    private static readonly Guid SystemUserId = EmailIngestActorResolver.SystemUserId;

    private readonly ITenantContext _tenantContext;
    private readonly ITenantConnectionProvider _connectionProvider;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IRepositoryUploadIndexService _uploadIndex;
    private readonly OcrToFormDataMapper _ocrMapper;
    private readonly IMediator _mediator;
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailIngestNormalWorkflowStarter> _logger;

    public EmailIngestNormalWorkflowStarter(
        ITenantContext tenantContext,
        ITenantConnectionProvider connectionProvider,
        ICurrentUserProvider currentUserProvider,
        IRepositoryUploadIndexService uploadIndex,
        OcrToFormDataMapper ocrMapper,
        IMediator mediator,
        IConfiguration configuration,
        ILogger<EmailIngestNormalWorkflowStarter> logger)
    {
        _tenantContext = tenantContext;
        _connectionProvider = connectionProvider;
        _currentUserProvider = currentUserProvider;
        _uploadIndex = uploadIndex;
        _ocrMapper = ocrMapper;
        _mediator = mediator;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<StartWorkflowCommandResult> StartAsync(
        WorkflowEntity workflow,
        byte[] attachmentBytes,
        string fileName,
        string? contentType,
        string contextJson,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant context is required.");

        if (string.IsNullOrWhiteSpace(workflow.RepositoryId))
            throw new InvalidOperationException("Workflow RepositoryId is not configured. Set InitiateUsing.RepositoryId on the workflow.");

        if (string.IsNullOrWhiteSpace(workflow.FormId))
            throw new InvalidOperationException("Workflow FormId is not configured. Set InitiateUsing.FormId on the workflow.");

        var connectionString = _tenantContext.ConnectionString
            ?? _connectionProvider.ConnectionString
            ?? throw new InvalidOperationException("Tenant connection string not resolved.");

        var repositoryGuid = await ResolveRepositoryGuidAsync(
            connectionString,
            tenantId,
            workflow.RepositoryId,
            cancellationToken)
            ?? throw new InvalidOperationException($"Could not resolve repository id '{workflow.RepositoryId}'.");

        await using var ocrStream = new MemoryStream(attachmentBytes, writable: false);
        var ocrResult = await _uploadIndex.UploadForOcrAsync(
            repositoryGuid,
            tenantId,
            ocrStream,
            fieldsJson: null,
            pageNo: null,
            ocrType: null,
            validateType: null,
            filename: fileName,
            cancellationToken);

        var metadataPayload = new Dictionary<string, object?>
        {
            ["ocrJson"] = ocrResult.OcrJson,
            ["ocrFieldList"] = ocrResult.OcrFieldList
        };

        if (!string.IsNullOrWhiteSpace(ocrResult.OcrText))
            metadataPayload["ocrText"] = ocrResult.OcrText;

        if (EmailIngestActorResolver.TryParseEmailIngestSender(
                contextJson,
                out var senderEmail,
                out _)
            && !string.IsNullOrWhiteSpace(senderEmail))
        {
            metadataPayload["CreatedByEmail"] = senderEmail.Trim();
            metadataPayload["CreatedByName"] = senderEmail.Trim();
        }

        var metadataJson = JsonSerializer.Serialize(metadataPayload);

        var actorUserId = _currentUserProvider.GetUserId() ?? SystemUserId;

        await using var stageStream = new MemoryStream(attachmentBytes, writable: false);
        var stageResult = await _uploadIndex.UploadWithOcrAsync(
            repositoryGuid,
            tenantId,
            stageStream,
            fileName,
            contentType,
            attachmentBytes.Length,
            metadataJson,
            ocrResult.OcrJson,
            ocrText: ocrResult.OcrText,
            actorUserId,
            cancellationToken);

        if (!Guid.TryParse(stageResult.FileId, out var stageFileId) || stageFileId == Guid.Empty)
            throw new InvalidOperationException("uploadWithOcr did not return a valid stage fileId.");

        var formData = await _ocrMapper.MapAsync(
            workflow.FormId,
            stageResult.OcrFieldList ?? ocrResult.OcrFieldList,
            cancellationToken);

        _logger.LogInformation(
            "Email ingest normal start: workflow {WorkflowId}, repository {RepositoryId}, stage {StageId}, formFields={FieldCount}",
            workflow.Id,
            repositoryGuid,
            stageFileId,
            formData.Count);

        var envType = _configuration["WorkflowStart:EnvType"] ?? "trial";

        return await _mediator.Send(new StartWorkflowCommand(
            workflow.Id,
            Context: contextJson,
            EnvType: envType,
            Attachment: null,
            TriggerApAgentPythonJob: false,
            FormDataFields: formData,
            StagedFiles: [new StartWorkflowStagedFileRef(repositoryGuid, stageFileId)]),
            cancellationToken);
    }

    private static async Task<Guid?> ResolveRepositoryGuidAsync(
        string connectionString,
        Guid tenantGuid,
        string? repositoryIdLink,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(repositoryIdLink))
            return null;

        var trimmed = repositoryIdLink.Trim();
        if (Guid.TryParse(trimmed, out var parsed))
            return parsed;

        if (trimmed.Length == 32 && Guid.TryParseExact(trimmed, "N", out parsed))
            return parsed;

        if (!int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var legacyInt))
            return null;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        const string byTableSql = """
            SELECT TOP 1 Id
            FROM repository.Repositories
            WHERE TenantId = @TenantId AND IsDeleted = 0
              AND (ItemsTableName LIKE @LegacyPattern OR StageTableName LIKE @LegacyPattern);
            """;

        var legacyPattern = $"%_{legacyInt}_%";
        await using var cmd = new SqlCommand(byTableSql, connection);
        cmd.Parameters.AddWithValue("@TenantId", tenantGuid);
        cmd.Parameters.AddWithValue("@LegacyPattern", legacyPattern);
        var o = await cmd.ExecuteScalarAsync(cancellationToken);
        return o is Guid g ? g : null;
    }
}
