using System.Text;
using System.Text.Json;

namespace SaaSApp.Workflow.Application.Contracts;

/// <summary>Python AP Agent expects POST body: { "startPayload": { blobPath, workflowId, ... } }.</summary>
public static class ApAgentStartPayloadJson
{
    public static string UnwrapInner(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return json;

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            return json;

        if (TryGetNestedStartPayload(root, out var inner))
            return inner.GetRawText();

        return json;
    }

    public static string WrapForPythonApi(string innerFlatJson)
    {
        var inner = JsonDocument.Parse(innerFlatJson).RootElement;
        if (inner.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("Start payload must be a JSON object.", nameof(innerFlatJson));

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WritePropertyName("startPayload");
            inner.WriteTo(writer);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static bool TryGetNestedStartPayload(JsonElement root, out JsonElement startPayload)
    {
        if (root.TryGetProperty("startPayload", out startPayload) && startPayload.ValueKind == JsonValueKind.Object)
            return true;

        if (root.TryGetProperty("StartPayload", out startPayload) && startPayload.ValueKind == JsonValueKind.Object)
            return true;

        startPayload = default;
        return false;
    }

    /// <summary>Adds job tracking fields for Python progress callbacks (ignored if already present).</summary>
    public static string EnrichWithJobTracking(
        string innerFlatJson,
        Guid workflowId,
        Guid instanceId,
        string apAgentJobId,
        string? apiBaseUrl)
    {
        using var doc = JsonDocument.Parse(innerFlatJson);
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
            return innerFlatJson;

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.NameEquals("apAgentJobId")
                    || prop.NameEquals("apAgentJobStatusUrl")
                    || prop.NameEquals("apAgentProgressUrl")
                    || prop.NameEquals("workflowId")
                    || prop.NameEquals("instanceId"))
                {
                    continue;
                }

                prop.WriteTo(writer);
            }

            writer.WriteString("apAgentJobId", apAgentJobId);
            writer.WriteString("workflowId", workflowId.ToString("D"));
            writer.WriteString("instanceId", instanceId.ToString("D"));

            if (!string.IsNullOrWhiteSpace(apiBaseUrl))
            {
                var baseUrl = apiBaseUrl.TrimEnd('/');
                writer.WriteString("apAgentJobStatusUrl", $"{baseUrl}/ap-agent/jobs/{apAgentJobId}");
                writer.WriteString(
                    "apAgentProgressUrl",
                    $"{baseUrl}/{workflowId:D}/instances/{instanceId:D}/ap-agent/progress");
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
