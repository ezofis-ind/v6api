using SaaSApp.Repository.Application.Contracts;

namespace SaaSApp.Repository.Infrastructure;

/// <summary>Merges metadata into <see cref="CreateRepositoryItemRequest.FieldValues"/> (no AP column renaming).</summary>
internal static class RepositoryItemMetadataMerger
{
    public static CreateRepositoryItemRequest Apply(
        CreateRepositoryItemRequest request,
        IReadOnlyDictionary<string, string> metadata)
    {
        if (metadata.Count == 0)
            return request;

        var merged = new Dictionary<string, string>(metadata, StringComparer.OrdinalIgnoreCase);
        if (request.FieldValues != null)
        {
            foreach (var (key, value) in request.FieldValues)
                merged[key] = value;
        }

        return request with { FieldValues = merged };
    }
}
