namespace SaaSApp.Repository.Infrastructure.Options;

public sealed class RepositoryFileStorageOptions
{
    public const string SectionName = "Repository:FileStorage";

    /// <summary>Local disk fallback when EzofisBlobStorage is not configured. Path: {root}/{tenantId}/repository/{repositoryId}/{itemId}/.</summary>
    public string LocalRootPath { get; set; } = Path.Combine(Path.GetTempPath(), "ezofis-repository");
}
