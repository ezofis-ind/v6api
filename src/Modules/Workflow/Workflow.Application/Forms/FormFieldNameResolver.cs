namespace SaaSApp.Workflow.Application.Forms;

/// <summary>
/// Resolves display names from designer form fields.
/// Top-level controls use <c>label</c>; TABLE columns use <c>name</c>.
/// </summary>
public static class FormFieldNameResolver
{
    public static string ResolveDisplayName(FormFieldDto field)
    {
        foreach (var candidate in new[] { field.Label, field.Name, field.Id })
        {
            if (!string.IsNullOrWhiteSpace(candidate))
                return candidate.Trim();
        }

        return string.Empty;
    }

    public static Dictionary<string, string> BuildJsonIdLabelIndex(FormJsonDto? formJson)
    {
        var index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (formJson == null)
            return index;

        foreach (var panel in formJson.Panels ?? [])
            WalkFields(panel.Fields, index);

        foreach (var panel in formJson.SecondaryPanels ?? [])
            WalkFields(panel.Fields, index);

        return index;
    }

    private static void WalkFields(IEnumerable<FormFieldDto>? fields, Dictionary<string, string> index)
    {
        if (fields == null)
            return;

        foreach (var field in fields)
        {
            if (!string.IsNullOrWhiteSpace(field.Id))
            {
                var displayName = ResolveDisplayName(field);
                if (!string.IsNullOrWhiteSpace(displayName) && !LooksLikeJsonId(displayName))
                    index[field.Id.Trim()] = displayName;
            }

            var tableColumns = field.Settings?.Specific?.TableColumns;
            if (tableColumns is { Count: > 0 })
                WalkFields(tableColumns, index);
        }
    }

    public static bool LooksLikeJsonId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return Guid.TryParse(value.Trim(), out _);
    }

    /// <summary>TABLE jsonId → display label (e.g. Cost Details).</summary>
    public static Dictionary<string, string> BuildTableJsonIdToLabel(FormJsonDto? formJson)
    {
        var index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (formJson == null)
            return index;

        foreach (var panel in formJson.Panels ?? [])
            WalkTables(panel.Fields, index);

        foreach (var panel in formJson.SecondaryPanels ?? [])
            WalkTables(panel.Fields, index);

        return index;
    }

    /// <summary>Column jsonId → parent TABLE display label.</summary>
    public static Dictionary<string, string> BuildColumnJsonIdToTableLabel(FormJsonDto? formJson)
    {
        var index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (formJson == null)
            return index;

        foreach (var panel in formJson.Panels ?? [])
            WalkTableColumns(panel.Fields, index);

        foreach (var panel in formJson.SecondaryPanels ?? [])
            WalkTableColumns(panel.Fields, index);

        return index;
    }

    private static void WalkTables(IEnumerable<FormFieldDto>? fields, Dictionary<string, string> index)
    {
        if (fields == null)
            return;

        foreach (var field in fields)
        {
            if (string.Equals(field.Type, "TABLE", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(field.Id))
            {
                var label = ResolveDisplayName(field);
                if (!string.IsNullOrWhiteSpace(label))
                    index[field.Id.Trim()] = label;
            }

            if (field.Settings?.Specific?.TableColumns is { Count: > 0 } cols)
                WalkTables(cols, index);
        }
    }

    private static void WalkTableColumns(IEnumerable<FormFieldDto>? fields, Dictionary<string, string> index)
    {
        if (fields == null)
            return;

        foreach (var field in fields)
        {
            if (string.Equals(field.Type, "TABLE", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(field.Id))
            {
                var tableLabel = ResolveDisplayName(field);
                if (string.IsNullOrWhiteSpace(tableLabel))
                    continue;

                foreach (var col in field.Settings?.Specific?.TableColumns ?? [])
                {
                    if (!string.IsNullOrWhiteSpace(col.Id))
                        index[col.Id.Trim()] = tableLabel;
                }
            }

            if (field.Settings?.Specific?.TableColumns is { Count: > 0 } cols)
                WalkTableColumns(cols, index);
        }
    }
}
