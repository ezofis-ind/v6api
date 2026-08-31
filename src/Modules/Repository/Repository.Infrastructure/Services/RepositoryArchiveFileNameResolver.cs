using SaaSApp.Repository.Application.Contracts;
using SaaSApp.Repository.Infrastructure.Storage;

namespace SaaSApp.Repository.Infrastructure.Services;

/// <summary>
/// Archive blob/file name: folder path from <see cref="RepositoryFieldDto.IncludeInFolderStructure"/>
/// except the naming field. Prefer a non-folder field with Level above folder max; otherwise the
/// highest folder-structure field is the file stem (not a folder segment).
/// </summary>
internal static class RepositoryArchiveFileNameResolver
{
    public static RepositoryFieldDto? ResolveNamingField(
        IReadOnlyList<RepositoryFieldDto> allFields,
        IReadOnlyList<RepositoryFieldDto> orderedFolderFields)
    {
        var folderMaxLevel = orderedFolderFields.Count > 0
            ? orderedFolderFields.Max(f => f.Level)
            : -1;

        var dedicated = allFields
            .Where(f => !f.IncludeInFolderStructure && f.Level > folderMaxLevel)
            .OrderByDescending(f => f.Level)
            .ThenBy(f => f.OrderId ?? int.MaxValue)
            .ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        if (dedicated != null)
            return dedicated;

        // No level above folders → highest IncludeInFolderStructure field is the file name.
        if (orderedFolderFields.Count == 0)
            return null;

        return orderedFolderFields[^1];
    }

    /// <summary>Folder path levels only (excludes naming field when it is a folder-structure field).</summary>
    public static IReadOnlyList<RepositoryFieldDto> PathFolderFields(
        IReadOnlyList<RepositoryFieldDto> allFields,
        IReadOnlyList<RepositoryFieldDto> orderedFolderFields)
    {
        var naming = ResolveNamingField(allFields, orderedFolderFields);
        if (naming == null || !naming.IncludeInFolderStructure)
            return orderedFolderFields;

        return orderedFolderFields
            .Where(f => !string.Equals(f.SqlColumnName, naming.SqlColumnName, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public static string? ResolveArchiveFileStem(
        IReadOnlyList<RepositoryFieldDto> allFields,
        IReadOnlyDictionary<string, string> metadata)
    {
        var folderFields = RepositoryFolderStructureHelper.OrderFolderFields(
            allFields.Where(f => f.IncludeInFolderStructure));
        var namingField = ResolveNamingField(allFields, folderFields);
        if (namingField == null)
            return null;

        return RepositoryFolderMetadataResolver.ResolveSegmentName(metadata, namingField);
    }

    public static string ResolveArchiveBaseFileName(
        IReadOnlyList<RepositoryFieldDto> allFields,
        IReadOnlyDictionary<string, string> metadata,
        string originalFileName,
        string? contentType = null)
    {
        var stem = ResolveArchiveFileStem(allFields, metadata);
        var ext = ResolvePreferredExtension(originalFileName, contentType);

        if (string.IsNullOrWhiteSpace(stem))
            return RepositoryFilePathHelper.EnsureFileNameHasExtension(
                RepositoryFilePathHelper.GetBaseFileName(originalFileName),
                contentType,
                filePath: originalFileName);

        // Naming metadata is a stem (invoice/PO no.). Strip any accidental extension before appending.
        var rawStem = Path.GetFileNameWithoutExtension(stem);
        if (string.IsNullOrWhiteSpace(rawStem))
            rawStem = stem;
        stem = RepositoryFilePathHelper.SanitizePathSegment(rawStem);
        if (string.IsNullOrWhiteSpace(stem))
            return RepositoryFilePathHelper.EnsureFileNameHasExtension(
                RepositoryFilePathHelper.GetBaseFileName(originalFileName),
                contentType,
                filePath: originalFileName);

        return RepositoryFilePathHelper.EnsureFileNameHasExtension($"{stem}{ext}", contentType, filePath: originalFileName);
    }

    private static string ResolvePreferredExtension(string originalFileName, string? contentType)
    {
        var fromName = Path.GetExtension(originalFileName);
        var fromMime = ExtensionFromContentType(contentType);

        if (!string.IsNullOrEmpty(fromMime))
        {
            if (string.IsNullOrEmpty(fromName) || fromName == ".")
                return fromMime;

            if (!ExtensionsMatch(fromName, fromMime))
                return fromMime;
        }

        if (string.IsNullOrEmpty(fromName) || fromName == ".")
            return ".pdf";

        return fromName.ToLowerInvariant();
    }

    private static string? ExtensionFromContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
            return null;

        var mime = contentType.Trim().Split(';')[0].Trim().ToLowerInvariant();
        return mime switch
        {
            "application/pdf" => ".pdf",
            "image/tiff" => ".tiff",
            "image/tif" => ".tif",
            "image/jpeg" or "image/jpg" => ".jpg",
            "image/png" => ".png",
            "application/msword" => ".doc",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => ".docx",
            "application/vnd.ms-excel" => ".xls",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => ".xlsx",
            _ when mime.StartsWith("image/", StringComparison.Ordinal) => ".img",
            _ => null
        };
    }

    private static bool ExtensionsMatch(string? ext1, string? ext2)
    {
        if (string.IsNullOrEmpty(ext1) || string.IsNullOrEmpty(ext2))
            return false;

        var n1 = ext1.Trim().ToLowerInvariant() switch
        {
            ".jpeg" => ".jpg",
            ".tif" => ".tiff",
            _ => ext1.Trim().ToLowerInvariant()
        };
        var n2 = ext2.Trim().ToLowerInvariant() switch
        {
            ".jpeg" => ".jpg",
            ".tif" => ".tiff",
            _ => ext2.Trim().ToLowerInvariant()
        };

        return string.Equals(n1, n2, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Naming metadata is preferred for the archive file stem; if missing, callers fall back to the
    /// original upload file name via <see cref="ResolveArchiveBaseFileName"/>. Do not throw here —
    /// workflow start/promote often has incomplete repo metadata (form GUID keys ≠ repo fields).
    /// </summary>
    public static void EnsureMandatoryNamingMetadata(
        IReadOnlyList<RepositoryFieldDto> allFields,
        IReadOnlyDictionary<string, string> metadata)
    {
        // Intentionally no-op: missing naming values are handled by ResolveArchiveBaseFileName fallback.
        _ = allFields;
        _ = metadata;
    }
}
