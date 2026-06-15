using System.Text.Json;
using SaaSApp.Workflow.Application.Workflows;

namespace SaaSApp.Workflow.Application.Forms;

/// <summary>Maps designer JSON (including panels-only payloads) into <see cref="FormJsonDto"/> for create.</summary>
public static class FormJsonNormalizer
{
    private static readonly JsonSerializerOptions DeserializeOptions = WorkflowJsonSerializerOptions.Deserialize;

    public static FormJsonDto NormalizeForCreate(JsonElement root)
    {
        if (TryGetProperty(root, "settings", out var settingsEl) && settingsEl.ValueKind == JsonValueKind.Object)
        {
            return JsonSerializer.Deserialize<FormJsonDto>(root.GetRawText(), DeserializeOptions)
                ?? throw new InvalidOperationException("Invalid form JSON.");
        }

        var name = ResolveFormName(root);
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException(
                "Form name is required. Set settings.general.name in the request body, or include a top-level \"name\" property.");

        return new FormJsonDto
        {
            Panels = DeserializePanels(root, "panels"),
            SecondaryPanels = DeserializePanels(root, "secondaryPanels"),
            Settings = new FormSettingsDto
            {
                General = new FormGeneralDto
                {
                    Name = name,
                    Description = "",
                    Layout = "SINGLE",
                    Type = "ENTRY"
                },
                Publish = new FormPublishDto { PublishOption = "DRAFT" }
            },
            IsDeleted = false
        };
    }

    public static string ResolveFormName(JsonElement root)
    {
        if (TryGetProperty(root, "settings", out var settings) &&
            TryGetProperty(settings, "general", out var general) &&
            TryGetProperty(general, "name", out var nameEl) &&
            nameEl.ValueKind == JsonValueKind.String)
        {
            var n = nameEl.GetString()?.Trim();
            if (!string.IsNullOrWhiteSpace(n))
                return n!;
        }

        if (TryGetProperty(root, "name", out var rootName) && rootName.ValueKind == JsonValueKind.String)
        {
            var n = rootName.GetString()?.Trim();
            if (!string.IsNullOrWhiteSpace(n))
                return n!;
        }

        if (TryGetProperty(root, "panels", out var panels) && panels.ValueKind == JsonValueKind.Array)
        {
            foreach (var panel in panels.EnumerateArray())
            {
                if (!TryGetProperty(panel, "settings", out var panelSettings))
                    continue;
                if (TryGetProperty(panelSettings, "title", out var title) && title.ValueKind == JsonValueKind.String)
                {
                    var t = title.GetString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(t))
                        return t!;
                }
            }
        }

        return "Untitled Form";
    }

    private static List<FormPanelDto>? DeserializePanels(JsonElement root, string propertyName)
    {
        if (!TryGetProperty(root, propertyName, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return null;

        return JsonSerializer.Deserialize<List<FormPanelDto>>(arr.GetRawText(), DeserializeOptions);
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
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
}
