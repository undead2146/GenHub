using System.Collections.Generic;

namespace GenHub.Core.Models.Workspace;

/// <summary>
/// Contains information about workspace cleanup operations that require user confirmation.
/// </summary>
public class WorkspaceCleanupConfirmation
{
    /// <summary>
    /// Gets or sets the number of files that will be removed.
    /// </summary>
    public int FilesToRemove { get; set; }

    /// <summary>
    /// Gets or sets the total size in bytes of files to be removed.
    /// </summary>
    public long TotalSizeBytes { get; set; }

    /// <summary>
    /// Gets or sets the list of content manifest names/IDs that will be affected.
    /// </summary>
    public List<string> AffectedManifests { get; set; } = [];

    /// <summary>
    /// Gets or sets the workspace deltas representing the removal operations.
    /// </summary>
    public List<WorkspaceDelta> RemovalDeltas { get; set; } = [];

    /// <summary>
    /// Gets a value indicating whether cleanup is needed (has files to remove).
    /// </summary>
    public bool IsCleanupNeeded => FilesToRemove > 0;
}
