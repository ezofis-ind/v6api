using System.Text.Json;
using System.Text.Json.Nodes;
using SaaSApp.Workflow.Domain.Enums;

namespace SaaSApp.Workflow.Application.Workflows;

/// <summary>Aligns designer JSON metadata with current workflow row values on GET.</summary>
internal static class WorkflowJsonDbSyncHelper
{
    internal static JsonElement? ApplyDbMetadata(
        JsonElement? workflowJson,
        string name,
        string? description,
        WorkflowStatus status)
    {
        if (workflowJson is not { ValueKind: JsonValueKind.Object } element)
            return workflowJson;

        var node = JsonNode.Parse(element.GetRawText())?.AsObject();
        if (node is null)
            return workflowJson;

        var settings = GetOrCreateObject(node, "settings");
        var general = GetOrCreateObject(settings, "general");
        general["name"] = name;
        if (description != null)
            general["description"] = description;

        var publish = GetOrCreateObject(settings, "publish");
        publish["publishOption"] = status == WorkflowStatus.Active ? "PUBLISHED" : "DRAFT";

        using var document = JsonDocument.Parse(node.ToJsonString());
        return document.RootElement.Clone();
    }

    private static JsonObject GetOrCreateObject(JsonObject parent, string propertyName)
    {
        foreach (var prop in parent)
        {
            if (prop.Key.Equals(propertyName, StringComparison.OrdinalIgnoreCase) &&
                prop.Value is JsonObject existing)
                return existing;
        }

        var created = new JsonObject();
        parent[propertyName] = created;
        return created;
    }
}
