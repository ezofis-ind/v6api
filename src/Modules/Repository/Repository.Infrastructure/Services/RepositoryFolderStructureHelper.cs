using SaaSApp.Repository.Application.Contracts;

namespace SaaSApp.Repository.Infrastructure.Services;

/// <summary>
/// Ordered repository fields that participate in archive folder paths
/// (<see cref="RepositoryFieldDto.IncludeInFolderStructure"/>).
/// The uploaded file name is always appended after these levels in blob storage.
/// </summary>
internal static class RepositoryFolderStructureHelper
{
    public static IReadOnlyList<RepositoryFieldDto> OrderFolderFields(IEnumerable<RepositoryFieldDto> fields) =>
        fields
            .OrderBy(f => f.Level)
            .ThenBy(f => f.OrderId ?? int.MaxValue)
            .ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
