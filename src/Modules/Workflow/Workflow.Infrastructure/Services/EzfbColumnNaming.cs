namespace SaaSApp.Workflow.Infrastructure.Services;

/// <summary>Maps designer jsonId to dbo.ezfb_* column names (bracket identifiers).</summary>
public static class EzfbColumnNaming
{
    /// <summary>jsonId → SQL column name: letters, digits, underscore, hyphen (matches designer field id).</summary>
    public static string ToColumnName(string jsonId)
    {
        var safe = new string(jsonId.Where(static c => char.IsLetterOrDigit(c) || c is '_' or '-').ToArray());
        if (string.IsNullOrEmpty(safe))
            throw new ArgumentException($"Invalid jsonId for ezfb column: {jsonId}");

        return safe;
    }

    public static bool TryToColumnName(string jsonId, out string column)
    {
        try
        {
            column = ToColumnName(jsonId);
            return true;
        }
        catch (ArgumentException)
        {
            column = string.Empty;
            return false;
        }
    }

    /// <summary>Bracket-escaped column name for dynamic SQL.</summary>
    public static string ToSqlBracketIdentifier(string jsonId) =>
        ToColumnName(jsonId).Replace("]", "]]", StringComparison.Ordinal);
}
