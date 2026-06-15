namespace SaaSApp.Workflow.Infrastructure.Services;

/// <summary>
/// Form ids: full GUID (dashed) in dbo.wForm / wFormControl; first 8 hex chars for ezfb_{suffix}_items tables.
/// </summary>
public static class FormIdNaming
{
    public const int EzfbTableSuffixLength = 8;

    /// <summary>New dbo.wForm.id — standard GUID with hyphens, e.g. fe0596e0-7780-42e6-a585-acc9caefa18e.</summary>
    public static string GenerateFormId() => Guid.NewGuid().ToString("D");

    /// <summary>Normalize stored form id to dashed GUID when possible.</summary>
    public static string NormalizeFormId(string formId)
    {
        var trimmed = formId.Trim();
        return Guid.TryParse(trimmed, out var guid) ? guid.ToString("D") : trimmed;
    }

    /// <summary>Suffix for dbo.ezfb_{suffix}_items (first 8 alphanumeric chars of form id, lowercase).</summary>
    public static string GetEzfbTableSuffix(string formId)
    {
        var compact = string.Concat(formId.Where(char.IsLetterOrDigit));
        if (compact.Length == 0)
            throw new InvalidOperationException($"Invalid form id for ezfb table suffix: '{formId}'.");

        return compact.Length <= EzfbTableSuffixLength
            ? compact.ToLowerInvariant()
            : compact[..EzfbTableSuffixLength].ToLowerInvariant();
    }
}
