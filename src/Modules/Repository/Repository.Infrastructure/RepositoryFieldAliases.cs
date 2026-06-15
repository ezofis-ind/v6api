namespace SaaSApp.Repository.Infrastructure;

/// <summary>
/// Normalizes field/column names for SQL (no semantic renaming).
/// Display labels in <c>RepositoryFields.Name</c> stay exactly as submitted.
/// </summary>
internal static class RepositoryFieldAliases
{
    public static string Canonicalize(string fieldOrColumnName) =>
        RepositorySqlHelper.SanitizeColumnName(fieldOrColumnName);
}
