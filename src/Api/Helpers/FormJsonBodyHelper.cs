using System.Text.Json;
using SaaSApp.Workflow.Application.Forms;

namespace SaaSApp.Api.Helpers;

/// <summary>Detects designer form JSON (camelCase or PascalCase) for POST /api/form.</summary>
internal static class FormJsonBodyHelper
{
    private static readonly JsonSerializerOptions DeserializeOptions =
        SaaSApp.Workflow.Application.Workflows.WorkflowJsonSerializerOptions.Deserialize;

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
        TryGetPropertyIgnoreCase(root, "panels", out _) ||
        TryGetPropertyIgnoreCase(root, "settings", out _);

    internal static bool HasFormJsonWrapper(JsonElement root) =>
        TryGetPropertyIgnoreCase(root, "formJson", out _) ||
        TryGetPropertyIgnoreCase(root, "fformJson", out _);

    internal static FormJsonDto? DeserializeDesignerJson(string raw) =>
        JsonSerializer.Deserialize<FormJsonDto>(raw, DeserializeOptions);

    internal static FormJsonDto NormalizeForCreate(JsonElement root) =>
        FormJsonNormalizer.NormalizeForCreate(root);

    /// <summary>Designer JSON text to persist in blob (preserves camelCase and extra properties).</summary>
    internal static string? ExtractDesignerJsonRaw(JsonElement root)
    {
        if (TryGetPropertyIgnoreCase(root, "formJson", out var formJson) &&
            formJson.ValueKind == JsonValueKind.Object)
            return formJson.GetRawText();

        if (TryGetPropertyIgnoreCase(root, "fformJson", out var fformJson) &&
            fformJson.ValueKind == JsonValueKind.Object)
            return fformJson.GetRawText();

        if (IsDesignerPayload(root))
            return root.GetRawText();

        return null;
    }
}
