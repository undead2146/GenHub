using System.Linq;
using GenHub.Core.Models.Manifest;
using GenHub.Core.Models.Workspace;

namespace GenHub.Core.Extensions;

public static class WorkspaceConfigurationExtensions
{
    /// <summary>
    /// Gets all unique files from all manifests, deduplicated by relative path.
    /// When multiple manifests contain the same file path, returns the first occurrence.
    /// </summary>
    public static IEnumerable<ManifestFile> GetAllUniqueFiles(
        this WorkspaceConfiguration configuration)
    {
        return configuration.Manifests
            .SelectMany(m => m.Files ?? [])
            .DistinctBy(
                f => f.RelativePath, 
                StringComparer.OrdinalIgnoreCase);
    }
}