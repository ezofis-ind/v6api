using System.Text.Json;
using System.Text.Json.Serialization;

namespace SaaSApp.Workflow.Application.Workflows;

/// <summary>JSON options for workflow designer blob storage (camelCase, matches frontend payloads).</summary>
public static class WorkflowJsonSerializerOptions
{
    public static JsonSerializerOptions Storage { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public static JsonSerializerOptions Deserialize { get; } = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
