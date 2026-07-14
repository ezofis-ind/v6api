using System.Text.Json;

namespace SaaSApp.ActivityLog.Application;

/// <summary>Allowlisted subject fields from a request body for Event Log titles.</summary>
public sealed record EventLogSubject(
    string? Email = null,
    string? DisplayName = null,
    string? RoleName = null,
    string? GroupName = null,
    string? Name = null,
    string? FileName = null)
{
    public bool HasAny =>
        !string.IsNullOrWhiteSpace(Email)
        || !string.IsNullOrWhiteSpace(DisplayName)
        || !string.IsNullOrWhiteSpace(RoleName)
        || !string.IsNullOrWhiteSpace(GroupName)
        || !string.IsNullOrWhiteSpace(Name)
        || !string.IsNullOrWhiteSpace(FileName);
}

/// <summary>Parses allowlisted JSON properties only (never passwords or other secrets).</summary>
public static class EventLogSubjectParser
{
    public static EventLogSubject Parse(ReadOnlySpan<byte> utf8Json)
    {
        if (utf8Json.IsEmpty)
            return new EventLogSubject();

        try
        {
            using var doc = JsonDocument.Parse(utf8Json.ToArray());
            return FromRoot(doc.RootElement);
        }
        catch
        {
            return new EventLogSubject();
        }
    }

    public static EventLogSubject Parse(ReadOnlySpan<char> json)
    {
        if (json.IsEmpty || json.IsWhiteSpace())
            return new EventLogSubject();

        try
        {
            using var doc = JsonDocument.Parse(json.ToString());
            return FromRoot(doc.RootElement);
        }
        catch
        {
            return new EventLogSubject();
        }
    }

    private static EventLogSubject FromRoot(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            return new EventLogSubject();

        var topName = GetStringIgnoreCase(root, "name");
        var nestedName = GetNestedName(root);
        var name = FirstNonEmpty(topName, nestedName);

        return new EventLogSubject(
            Email: GetStringIgnoreCase(root, "email"),
            DisplayName: GetStringIgnoreCase(root, "displayName"),
            RoleName: GetStringIgnoreCase(root, "roleName"),
            GroupName: GetStringIgnoreCase(root, "groupName"),
            Name: name,
            FileName: GetStringIgnoreCase(root, "fileName"));
    }

    /// <summary>Prefer settings.general.name for workflow/form designer JSON.</summary>
    private static string? GetNestedName(JsonElement root)
    {
        if (TryGetObjectIgnoreCase(root, "settings", out var settings)
            && TryGetObjectIgnoreCase(settings, "general", out var general))
        {
            return GetStringIgnoreCase(general, "name");
        }

        if (TryGetObjectIgnoreCase(root, "workflowJson", out var workflowJson)
            && TryGetObjectIgnoreCase(workflowJson, "settings", out var wjSettings)
            && TryGetObjectIgnoreCase(wjSettings, "general", out var wjGeneral))
        {
            return GetStringIgnoreCase(wjGeneral, "name");
        }

        return null;
    }

    private static bool TryGetObjectIgnoreCase(JsonElement root, string propertyName, out JsonElement obj)
    {
        foreach (var prop in root.EnumerateObject())
        {
            if (!prop.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
                continue;
            if (prop.Value.ValueKind == JsonValueKind.Object)
            {
                obj = prop.Value;
                return true;
            }
        }

        obj = default;
        return false;
    }

    private static string? GetStringIgnoreCase(JsonElement root, string propertyName)
    {
        foreach (var prop in root.EnumerateObject())
        {
            if (!prop.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
                continue;
            if (prop.Value.ValueKind != JsonValueKind.String)
                return null;

            var value = prop.Value.GetString();
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        return null;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return null;
    }
}
