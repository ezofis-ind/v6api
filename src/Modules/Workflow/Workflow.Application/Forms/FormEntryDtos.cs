using System.Text.Json;
using System.Text.Json.Serialization;

namespace SaaSApp.Workflow.Application.Forms;

/// <summary>v5-compatible form entry upsert result (maps to resultforHTTpscode).</summary>
public sealed record FormEntryResult(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("output")] string? Output,
    [property: JsonPropertyName("EncryptOutput")] string? EncryptOutput,
    [property: JsonPropertyName("creditconsumed")] bool CreditConsumed = false);

/// <summary>Duplicate unique-column violation details (v5 output when id=3).</summary>
public sealed record FormEntryDuplicateField(string Id, string? Name, string? Value);

/// <summary>v5 insformentry — field map keyed by wFormControl jsonId.</summary>
public sealed class FormEntryUpsertRequest
{
    public Dictionary<string, JsonElement>? Fields { get; set; }
}

public enum FormEntryGetStatus
{
    Found = 1,
    NotFound = 0
}

public sealed record FormEntryGetResult(
    FormEntryGetStatus Status,
    IReadOnlyList<Dictionary<string, object?>>? Entries);
