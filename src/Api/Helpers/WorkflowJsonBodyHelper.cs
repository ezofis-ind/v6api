using System.Text.Json;
using SaaSApp.Workflow.Application.Workflows.Commands.CreateWorkflow;

namespace SaaSApp.Api.Helpers;

/// <summary>Detects designer workflow JSON (PascalCase or camelCase) for POST/PUT /api/workflows.</summary>
internal static class WorkflowJsonBodyHelper
{
    private static readonly JsonSerializerOptions DeserializeOptions = SaaSApp.Workflow.Application.Workflows.WorkflowJsonSerializerOptions.Deserialize;

    internal static bool TryGetPropertyIgnoreCase(JsonElement element, string name, out JsonElement value)
    {
        value = default;
        if (element.ValueKind != JsonValueKind.Object)
            return false;

        foreach (var prop in element.EnumerateObject())
        {
            if (prop.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                value = prop.Value;
                return true;
            }
        }

        return false;
    }

    internal static bool IsDesignerPayload(JsonElement root) =>
        TryGetPropertyIgnoreCase(root, "blocks", out _) ||
        TryGetPropertyIgnoreCase(root, "settings", out _);

    internal static bool HasWorkflowJsonWrapper(JsonElement root) =>
        TryGetPropertyIgnoreCase(root, "workflowJson", out _);

    internal static WorkflowJsonDto? DeserializeDesignerJson(string raw) =>
        JsonSerializer.Deserialize<WorkflowJsonDto>(raw, DeserializeOptions);

    /// <summary>Original designer JSON text to persist in blob (preserves camelCase and extra properties).</summary>
    internal static string? ExtractDesignerJsonRaw(JsonElement root)
    {
        if (TryGetPropertyIgnoreCase(root, "workflowJson", out var nested) &&
            nested.ValueKind == JsonValueKind.Object)
            return nested.GetRawText();

        if (IsDesignerPayload(root))
            return root.GetRawText();

        return null;
    }

    internal static string ResolveWorkflowName(JsonElement root, WorkflowJsonDto? workflowJson)
    {
        var name = workflowJson?.Settings?.General?.Name?.Trim();
        if (!string.IsNullOrWhiteSpace(name))
            return name!;

        if (TryGetPropertyIgnoreCase(root, "name", out var rootName) &&
            rootName.ValueKind == JsonValueKind.String)
        {
            var n = rootName.GetString()?.Trim();
            if (!string.IsNullOrWhiteSpace(n))
                return n!;
        }

        return $"Workflow-{DateTime.UtcNow:yyyyMMddHHmmss}";
    }

    internal static bool IsPublished(WorkflowJsonDto? workflowJson, bool publishImmediatelyFlag = false)
    {
        var publishOption = workflowJson?.Settings?.Publish?.PublishOption;
        if (publishOption != null)
            return publishOption.Equals("PUBLISHED", StringComparison.OrdinalIgnoreCase);
        return publishImmediatelyFlag;
    }
}
