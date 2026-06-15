using System.Text.RegularExpressions;

namespace SaaSApp.Repository.Infrastructure.Storage;

internal static class RepositoryFilePathHelper
{
    public const string ArchiveRoot = "archive";

    /// <summary>Legacy flat path when archive layout is not used.</summary>
    public static string BuildFlatRelativePath(Guid repositoryId, Guid itemId, string fileName)
    {
        var ext = GetExtension(fileName);
        return $"repository/{repositoryId:N}/{itemId:N}{ext}";
    }

    /// <summary>
    /// Tenant container (ezts{tenantId}) + blob path:
    /// archive/{repositoryName}/{level1}/{level2}/{leafLevelName}.pdf
    /// The deepest folder level value is the file name (not item GUID).
    /// </summary>
    public static string BuildArchiveRelativePath(
        string repositoryName,
        IReadOnlyList<string> folderLevelNames,
        string originalFileName,
        int fileVersion = 1)
    {
        var ext = GetExtension(originalFileName);
        var repoSegment = SanitizePathSegment(repositoryName);
        if (string.IsNullOrWhiteSpace(repoSegment))
            repoSegment = "repository";

        if (folderLevelNames.Count == 0)
        {
            var fileSegment = SanitizePathSegment(Path.GetFileNameWithoutExtension(originalFileName));
            if (string.IsNullOrWhiteSpace(fileSegment))
                fileSegment = "document";
            return $"{ArchiveRoot}/{repoSegment}/{AppendVersionToFileSegment($"{fileSegment}{ext}", fileVersion)}";
        }

        var segments = new List<string> { ArchiveRoot, repoSegment };

        if (folderLevelNames.Count == 1)
        {
            var leaf = SanitizePathSegment(folderLevelNames[0]);
            if (string.IsNullOrWhiteSpace(leaf))
                leaf = "document";
            segments.Add(AppendVersionToFileSegment($"{leaf}{ext}", fileVersion));
            return string.Join('/', segments);
        }

        for (var i = 0; i < folderLevelNames.Count - 1; i++)
        {
            var folder = SanitizePathSegment(folderLevelNames[i]);
            if (string.IsNullOrWhiteSpace(folder))
                folder = $"Level{i + 1}";
            segments.Add(folder);
        }

        var fileName = SanitizePathSegment(folderLevelNames[^1]);
        if (string.IsNullOrWhiteSpace(fileName))
            fileName = "document";
        segments.Add(AppendVersionToFileSegment($"{fileName}{ext}", fileVersion));

        return string.Join('/', segments);
    }

    /// <summary>Original upload name without an existing <c>_vN</c> suffix.</summary>
    public static string GetBaseFileName(string fileName)
    {
        var name = Path.GetFileName(fileName.Trim());
        if (string.IsNullOrWhiteSpace(name))
            return name;

        var ext = Path.GetExtension(name);
        var stem = Path.GetFileNameWithoutExtension(name);
        if (string.IsNullOrWhiteSpace(stem))
            return name;

        stem = StripVersionSuffixFromStem(stem);
        return string.IsNullOrEmpty(ext) ? stem : stem + ext;
    }

    /// <summary>Display/storage name: <c>invoice_v1.pdf</c>, <c>invoice_v2.pdf</c>.</summary>
    public static string ApplyVersionToFileName(string fileName, int fileVersion)
    {
        if (fileVersion < 1)
            fileVersion = 1;

        var baseName = GetBaseFileName(fileName);
        var ext = Path.GetExtension(baseName);
        var stem = Path.GetFileNameWithoutExtension(baseName);
        if (string.IsNullOrWhiteSpace(stem))
            stem = "document";

        return $"{stem}_v{fileVersion}{ext}";
    }

    /// <summary>SQL LIKE pattern for all versions of a base file (e.g. <c>invoice_v%.pdf</c>).</summary>
    public static string BuildVersionedFileNameLikePattern(string baseFileName)
    {
        var baseName = GetBaseFileName(baseFileName);
        var ext = Path.GetExtension(baseName);
        var stem = Path.GetFileNameWithoutExtension(baseName);
        if (string.IsNullOrWhiteSpace(stem))
            stem = "document";

        return string.IsNullOrEmpty(ext)
            ? $"{stem}_v%"
            : $"{stem}_v%{ext}";
    }

    internal static string AppendVersionToFileSegment(string fileSegmentWithExt, int fileVersion)
    {
        if (fileVersion < 1)
            fileVersion = 1;

        var ext = Path.GetExtension(fileSegmentWithExt);
        var stem = Path.GetFileNameWithoutExtension(fileSegmentWithExt);
        if (string.IsNullOrWhiteSpace(stem))
            stem = "document";

        stem = StripVersionSuffixFromStem(stem);
        return $"{stem}_v{fileVersion}{ext}";
    }

    private static string StripVersionSuffixFromStem(string stem)
    {
        var match = VersionSuffixRegex.Match(stem);
        return match.Success ? stem[..match.Index] : stem;
    }

    private static readonly Regex VersionSuffixRegex = new(@"_v\d+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static string SanitizePathSegment(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0)
            return string.Empty;

        var invalid = Path.GetInvalidFileNameChars();
        var chars = trimmed
            .Select(c => invalid.Contains(c) || c is '/' or '\\' or ':' ? '_' : c)
            .ToArray();

        var cleaned = new string(chars).Trim().Trim('.');
        cleaned = Regex.Replace(cleaned, @"\s+", " ");
        return cleaned.Length > 200 ? cleaned[..200] : cleaned;
    }

    private static string GetExtension(string fileName)
    {
        var ext = Path.GetExtension(fileName);
        if (string.IsNullOrEmpty(ext))
            return ".pdf";
        return ext.ToLowerInvariant();
    }
}
