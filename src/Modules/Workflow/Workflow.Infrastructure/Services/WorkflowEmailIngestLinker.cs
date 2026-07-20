using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using SaaSApp.Workflow.Application.Connectors;
using SaaSApp.Workflow.Application.Contracts;
using SaaSApp.Workflow.Application.Workflows;
using SaaSApp.Workflow.Application.Workflows.Commands.CreateWorkflow;
using static SaaSApp.Workflow.Application.Workflows.WorkflowJsonSerializerOptions;

namespace SaaSApp.Workflow.Infrastructure.Services;

public sealed class WorkflowEmailIngestLinker : IWorkflowEmailIngestLinker
{
    private readonly IEmailIngestService _emailIngest;
    private readonly IConnectorService _connectorService;
    private readonly IWorkflowJsonStorageService _jsonStorage;
    private readonly ILogger<WorkflowEmailIngestLinker> _logger;

    public WorkflowEmailIngestLinker(
        IEmailIngestService emailIngest,
        IConnectorService connectorService,
        IWorkflowJsonStorageService jsonStorage,
        ILogger<WorkflowEmailIngestLinker> logger)
    {
        _emailIngest = emailIngest;
        _connectorService = connectorService;
        _jsonStorage = jsonStorage;
        _logger = logger;
    }

    public async Task<WorkflowEmailIngestLinkResult> SyncAsync(
        Guid workflowId,
        WorkflowJsonDto? workflowJson,
        string? workflowJsonRaw,
        WorkflowEmailIngestOptions? options,
        CancellationToken cancellationToken = default)
    {
        var json = workflowJson;
        if (json == null && !string.IsNullOrWhiteSpace(workflowJsonRaw))
        {
            json = JsonSerializer.Deserialize<WorkflowJsonDto>(workflowJsonRaw, Deserialize);
        }

        var existing = await _emailIngest.GetMailboxByWorkflowIdAsync(workflowId, cancellationToken);

        // Partial update with no email fields and no designer JSON: leave mailbox unchanged
        if (json == null && options == null)
        {
            return new WorkflowEmailIngestLinkResult(
                existing?.Id,
                existing?.ConnectorId,
                existing?.IsEnabled ?? false);
        }

        var isEmail = IsEmailInitiate(json, options);

        if (!isEmail)
        {
            if (existing is { IsEnabled: true })
            {
                await _emailIngest.UpdateMailboxAsync(existing.Id, ToUpsert(existing, isEnabled: false), cancellationToken);
                _logger.LogInformation("Disabled email ingest mailbox {MailboxId} for workflow {WorkflowId}", existing.Id, workflowId);
            }

            return new WorkflowEmailIngestLinkResult(
                existing?.Id,
                existing?.ConnectorId,
                false);
        }

        var connectorId = ResolveConnectorId(options, json);
        if (connectorId == null || connectorId == Guid.Empty)
            throw new InvalidOperationException(
                "emailConnectorId is required for email-initiated workflows. Connect Gmail/Outlook via OAuth and pass the connector Guid.");

        var connector = await _connectorService.GetByIdAsync(connectorId.Value, cancellationToken)
            ?? throw new InvalidOperationException("emailConnectorId connector not found.");
        var code = (connector.ProviderCode ?? string.Empty).Trim().ToUpperInvariant();
        if (code is not ("GMAIL" or "OUTLOOK"))
            throw new InvalidOperationException("emailConnectorId must be a GMAIL or OUTLOOK connector.");

        var masterSource = options?.MasterSource
            ?? existing?.MasterSource
            ?? (options?.MasterConnectorId != null
                ? EmailIngestMasterSources.QuickBooks
                : EmailIngestMasterSources.InternalForm);

        var upsert = new EmailIngestMailboxUpsertRequest(
            ConnectorId: connectorId.Value,
            WorkflowId: workflowId,
            IsEnabled: options?.EmailIsEnabled ?? true,
            PollIntervalMinutes: options?.EmailPollIntervalMinutes
                ?? existing?.PollIntervalMinutes
                ?? 5,
            QueryFilter: options?.EmailQueryFilter ?? existing?.QueryFilter,
            MasterSource: masterSource,
            MasterFormId: options?.MasterFormId ?? existing?.MasterFormId,
            MasterConnectorId: options?.MasterConnectorId ?? existing?.MasterConnectorId,
            AttachmentExtensions: existing?.AttachmentExtensions);

        // InternalForm requires masterFormId — if missing, default to a placeholder only when switching
        // from QuickBooks is not the case; validate via Create/Update which throws.
        if (string.Equals(upsert.MasterSource, EmailIngestMasterSources.InternalForm, StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(upsert.MasterFormId))
        {
            // Allow link without master form: use empty-safe path by setting QuickBooks-free InternalForm
            // only when master form is provided; otherwise keep prior or require caller to set form.
            if (existing != null && !string.IsNullOrWhiteSpace(existing.MasterFormId))
            {
                upsert = upsert with { MasterFormId = existing.MasterFormId };
            }
            else
            {
                throw new InvalidOperationException(
                    "masterFormId is required when masterSource is InternalForm (set on create/update with emailConnectorId).");
            }
        }

        EmailIngestMailboxDto mailbox;
        if (existing == null)
            mailbox = await _emailIngest.CreateMailboxAsync(upsert, cancellationToken);
        else
            mailbox = (await _emailIngest.UpdateMailboxAsync(existing.Id, upsert, cancellationToken))!;

        await PersistConnectorIdInWorkflowJsonAsync(workflowId, json, workflowJsonRaw, connectorId.Value, cancellationToken);

        return new WorkflowEmailIngestLinkResult(mailbox.Id, mailbox.ConnectorId, mailbox.IsEnabled);
    }

    private async Task PersistConnectorIdInWorkflowJsonAsync(
        Guid workflowId,
        WorkflowJsonDto? json,
        string? workflowJsonRaw,
        Guid connectorId,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(workflowJsonRaw))
        {
            try
            {
                var node = JsonNode.Parse(workflowJsonRaw);
                if (node is JsonObject root)
                {
                    PatchMailInitiateConnectorId(root, connectorId);
                    await _jsonStorage.SaveWorkflowJsonAsync(workflowId, root.ToJsonString(), cancellationToken);
                    return;
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Could not patch workflow JSON raw for MailInitiate.ConnectorId");
            }
        }

        if (json?.Blocks == null)
            return;

        var start = json.Blocks.FirstOrDefault(b =>
            string.Equals(b.Type, "START", StringComparison.OrdinalIgnoreCase));
        if (start?.Settings == null)
            return;

        var mail = start.Settings.MailInitiate ?? new WorkflowMailInitiateDto();
        mail = mail with { ConnectorId = new FlexibleWorkflowId(null, connectorId) };
        var newSettings = start.Settings with { MailInitiate = mail };
        var newStart = start with { Settings = newSettings };
        var blocks = json.Blocks.Select(b => b.Id == start.Id ? newStart : b).ToList();
        var updated = json with { Blocks = blocks };
        await _jsonStorage.SaveWorkflowJsonAsync(workflowId, JsonSerializer.Serialize(updated, Storage), cancellationToken);
    }

    private static void PatchMailInitiateConnectorId(JsonObject root, Guid connectorId)
    {
        if (!TryGetBlocks(root, out var blocks) || blocks is not JsonArray arr)
            return;

        foreach (var item in arr)
        {
            if (item is not JsonObject block)
                continue;
            var type = block["Type"]?.GetValue<string>() ?? block["type"]?.GetValue<string>();
            if (!string.Equals(type, "START", StringComparison.OrdinalIgnoreCase))
                continue;

            var settings = block["Settings"] as JsonObject ?? block["settings"] as JsonObject;
            if (settings == null)
            {
                settings = new JsonObject();
                if (block.ContainsKey("Settings"))
                    block["Settings"] = settings;
                else
                    block["settings"] = settings;
            }

            var mailKey = settings.ContainsKey("MailInitiate") ? "MailInitiate"
                : settings.ContainsKey("mailInitiate") ? "mailInitiate" : "MailInitiate";
            var mail = settings[mailKey] as JsonObject ?? new JsonObject();
            mail["ConnectorId"] = connectorId.ToString("D");
            settings[mailKey] = mail;
            return;
        }
    }

    private static bool TryGetBlocks(JsonObject root, out JsonNode? blocks)
    {
        if (root.TryGetPropertyValue("Blocks", out blocks) && blocks != null)
            return true;
        if (root.TryGetPropertyValue("blocks", out blocks) && blocks != null)
            return true;
        blocks = null;
        return false;
    }

    private static bool IsEmailInitiate(WorkflowJsonDto? json, WorkflowEmailIngestOptions? options)
    {
        if (options?.EmailConnectorId is { } id && id != Guid.Empty)
            return true;

        var type = json?.Settings?.General?.InitiateUsing?.Type;
        if (!string.IsNullOrWhiteSpace(type) &&
            type.Contains("EMAIL", StringComparison.OrdinalIgnoreCase))
            return true;

        var start = json?.Blocks?.FirstOrDefault(b =>
            string.Equals(b.Type, "START", StringComparison.OrdinalIgnoreCase));
        if (start?.Settings?.InitiateBy != null &&
            start.Settings.InitiateBy.Any(x => x.Contains("EMAIL", StringComparison.OrdinalIgnoreCase)))
            return true;

        return false;
    }

    private static Guid? ResolveConnectorId(WorkflowEmailIngestOptions? options, WorkflowJsonDto? json)
    {
        if (options?.EmailConnectorId is { } top && top != Guid.Empty)
            return top;

        var start = json?.Blocks?.FirstOrDefault(b =>
            string.Equals(b.Type, "START", StringComparison.OrdinalIgnoreCase));
        var flex = start?.Settings?.MailInitiate?.ConnectorId;
        if (flex == null)
            return null;
        if (flex.Value.Guid is Guid g && g != Guid.Empty)
            return g;
        if (flex.Value.LegacyInt is int)
            throw new InvalidOperationException(
                "MailInitiate.ConnectorId must be an OAuth connector Guid (not a legacy int). Re-connect Gmail/Outlook and pass emailConnectorId.");
        return null;
    }

    private static EmailIngestMailboxUpsertRequest ToUpsert(EmailIngestMailboxDto existing, bool isEnabled) =>
        new(
            existing.ConnectorId,
            existing.WorkflowId,
            isEnabled,
            existing.PollIntervalMinutes,
            existing.QueryFilter,
            existing.MasterSource,
            existing.MasterFormId,
            existing.MasterConnectorId,
            existing.AttachmentExtensions);
}
