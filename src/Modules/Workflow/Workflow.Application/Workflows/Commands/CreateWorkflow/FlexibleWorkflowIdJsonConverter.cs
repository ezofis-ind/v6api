using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SaaSApp.Workflow.Application.Workflows.Commands.CreateWorkflow;

/// <summary>Legacy numeric id or GUID (repository, form, etc.).</summary>
public readonly record struct FlexibleWorkflowId(int? LegacyInt, Guid? Guid);

/// <summary>Accepts id as JSON number (legacy) or string GUID.</summary>
public sealed class FlexibleWorkflowIdJsonConverter : JsonConverter<FlexibleWorkflowId?>
{
    public override FlexibleWorkflowId? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;
            case JsonTokenType.Number:
                if (reader.TryGetInt32(out var n))
                    return new FlexibleWorkflowId(n, null);
                break;
            case JsonTokenType.String:
                var s = reader.GetString();
                if (string.IsNullOrWhiteSpace(s))
                    return null;
                if (Guid.TryParseExact(s, "N", out var gn))
                    return new FlexibleWorkflowId(null, gn);
                if (Guid.TryParse(s, out var g))
                    return new FlexibleWorkflowId(null, g);
                if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
                    return new FlexibleWorkflowId(i, null);
                throw new JsonException($"Invalid id '{s}'. Expected a GUID or integer.");
        }

        throw new JsonException($"Unexpected JSON token {reader.TokenType} for id.");
    }

    public override void Write(Utf8JsonWriter writer, FlexibleWorkflowId? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        if (value.Value.Guid is Guid guid)
        {
            writer.WriteStringValue(guid.ToString());
            return;
        }

        if (value.Value.LegacyInt is int legacy)
        {
            writer.WriteNumberValue(legacy);
            return;
        }

        writer.WriteNullValue();
    }
}

/// <inheritdoc cref="FlexibleWorkflowId"/>
public readonly record struct FlexibleRepositoryId(int? LegacyInt, Guid? Guid);

/// <inheritdoc cref="FlexibleWorkflowIdJsonConverter"/>
public sealed class FlexibleRepositoryIdJsonConverter : JsonConverter<FlexibleRepositoryId?>
{
    private readonly FlexibleWorkflowIdJsonConverter _inner = new();

    public override FlexibleRepositoryId? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var v = _inner.Read(ref reader, typeof(FlexibleWorkflowId?), options);
        return v is null ? null : new FlexibleRepositoryId(v.Value.LegacyInt, v.Value.Guid);
    }

    public override void Write(Utf8JsonWriter writer, FlexibleRepositoryId? value, JsonSerializerOptions options) =>
        _inner.Write(writer, value is null ? null : new FlexibleWorkflowId(value.Value.LegacyInt, value.Value.Guid), options);
}
