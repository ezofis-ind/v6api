using SaaSApp.Workflow.Application.Connectors;
using SaaSApp.Workflow.Application.Workflows.Commands.CreateWorkflow;

namespace SaaSApp.Workflow.Application.Workflows;

/// <summary>Top-level email ingest options on workflow create/update.</summary>
public sealed record WorkflowEmailIngestOptions(
    Guid? EmailConnectorId = null,
    bool? EmailIsEnabled = null,
    int? EmailPollIntervalMinutes = null,
    string? EmailQueryFilter = null,
    string? MasterSource = null,
    string? MasterFormId = null,
    Guid? MasterConnectorId = null);

public sealed record WorkflowEmailIngestLinkResult(
    Guid? EmailIngestMailboxId,
    Guid? EmailConnectorId,
    bool EmailIngestEnabled);

/// <summary>Parse email ingest fields from API JSON root (wrapper or designer body).</summary>
public static class WorkflowEmailIngestOptionsHelper
{
    public static WorkflowEmailIngestOptions? FromJsonElement(System.Text.Json.JsonElement root)
    {
        if (root.ValueKind != System.Text.Json.JsonValueKind.Object)
            return null;

        Guid? connectorId = ReadGuid(root, "emailConnectorId") ?? ReadGuid(root, "EmailConnectorId");
        bool? enabled = ReadBool(root, "emailIsEnabled") ?? ReadBool(root, "EmailIsEnabled");
        int? poll = ReadInt(root, "emailPollIntervalMinutes") ?? ReadInt(root, "EmailPollIntervalMinutes");
        var query = ReadString(root, "emailQueryFilter") ?? ReadString(root, "EmailQueryFilter");
        var masterSource = ReadString(root, "masterSource") ?? ReadString(root, "MasterSource");
        var masterFormId = ReadString(root, "masterFormId") ?? ReadString(root, "MasterFormId");
        Guid? masterConnectorId = ReadGuid(root, "masterConnectorId") ?? ReadGuid(root, "MasterConnectorId");

        if (connectorId == null && enabled == null && poll == null && query == null
            && masterSource == null && masterFormId == null && masterConnectorId == null)
            return null;

        return new WorkflowEmailIngestOptions(
            connectorId, enabled, poll, query, masterSource, masterFormId, masterConnectorId);
    }

    public static WorkflowEmailIngestOptions? Merge(WorkflowEmailIngestOptions? fromBody, WorkflowEmailIngestOptions? fromRequest)
    {
        if (fromBody == null) return fromRequest;
        if (fromRequest == null) return fromBody;
        return new WorkflowEmailIngestOptions(
            fromRequest.EmailConnectorId ?? fromBody.EmailConnectorId,
            fromRequest.EmailIsEnabled ?? fromBody.EmailIsEnabled,
            fromRequest.EmailPollIntervalMinutes ?? fromBody.EmailPollIntervalMinutes,
            fromRequest.EmailQueryFilter ?? fromBody.EmailQueryFilter,
            fromRequest.MasterSource ?? fromBody.MasterSource,
            fromRequest.MasterFormId ?? fromBody.MasterFormId,
            fromRequest.MasterConnectorId ?? fromBody.MasterConnectorId);
    }

    public static WorkflowEmailIngestOptions? FromRequest(
        Guid? emailConnectorId,
        bool? emailIsEnabled,
        int? emailPollIntervalMinutes,
        string? emailQueryFilter,
        string? masterSource,
        string? masterFormId,
        Guid? masterConnectorId)
    {
        if (emailConnectorId == null && emailIsEnabled == null && emailPollIntervalMinutes == null
            && emailQueryFilter == null && masterSource == null && masterFormId == null && masterConnectorId == null)
            return null;
        return new WorkflowEmailIngestOptions(
            emailConnectorId, emailIsEnabled, emailPollIntervalMinutes, emailQueryFilter,
            masterSource, masterFormId, masterConnectorId);
    }

    private static Guid? ReadGuid(System.Text.Json.JsonElement root, string name)
    {
        if (!TryGetProperty(root, name, out var p))
            return null;
        if (p.ValueKind == System.Text.Json.JsonValueKind.String && Guid.TryParse(p.GetString(), out var g))
            return g;
        return null;
    }

    private static bool? ReadBool(System.Text.Json.JsonElement root, string name)
    {
        if (!TryGetProperty(root, name, out var p))
            return null;
        return p.ValueKind switch
        {
            System.Text.Json.JsonValueKind.True => true,
            System.Text.Json.JsonValueKind.False => false,
            System.Text.Json.JsonValueKind.String when bool.TryParse(p.GetString(), out var b) => b,
            _ => null
        };
    }

    private static int? ReadInt(System.Text.Json.JsonElement root, string name)
    {
        if (!TryGetProperty(root, name, out var p))
            return null;
        if (p.ValueKind == System.Text.Json.JsonValueKind.Number && p.TryGetInt32(out var n))
            return n;
        if (p.ValueKind == System.Text.Json.JsonValueKind.String && int.TryParse(p.GetString(), out var i))
            return i;
        return null;
    }

    private static string? ReadString(System.Text.Json.JsonElement root, string name)
    {
        if (!TryGetProperty(root, name, out var p) || p.ValueKind != System.Text.Json.JsonValueKind.String)
            return null;
        var s = p.GetString();
        return string.IsNullOrWhiteSpace(s) ? null : s;
    }

    private static bool TryGetProperty(System.Text.Json.JsonElement root, string name, out System.Text.Json.JsonElement value)
    {
        if (root.TryGetProperty(name, out value))
            return true;
        foreach (var prop in root.EnumerateObject())
        {
            if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = prop.Value;
                return true;
            }
        }
        value = default;
        return false;
    }
}
