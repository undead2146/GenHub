using System.Linq;
using GenHub.Core.Models.Manifest;
using GenHub.Core.Models.Workspace;

namespace GenHub.Core.Extensions;

/// <summary>
/// Provides extension methods for <see cref="WorkspaceConfiguration"/>.
/// </summary>
public static class WorkspaceConfigurationExtensions
{
    /// <summary>
    /// Gets all unique files from all manifests, deduplicated by relative path.
    /// When multiple manifests contain the same file path, returns the first occurrence.
    /// </summary>
    /// <param name="configuration">The workspace configuration to get files from.</param>
    /// <returns>An enumerable of unique manifest files.</returns>
    public static IEnumerable<ManifestFile> GetAllUniqueFiles(
        this WorkspaceConfiguration configuration)
    {
        return configuration.Manifests
            .SelectMany(m => (m.Files ?? []).Select(f => new { File = f, Manifest = m }))
            .GroupBy(x => x.File.RelativePath, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(x => ContentTypePriority.GetPriority(x.Manifest.ContentType))
                          .First().File);
    }
}