namespace GenHub.Core.Models.UserData;

/// <summary>
/// Information about user data that would be affected when switching profiles.
/// </summary>
public class UserDataSwitchInfo
{
    /// <summary>
    /// Gets or sets the old profile ID that has user data.
    /// </summary>
    public string OldProfileId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the number of files that would be removed.
    /// </summary>
    public int FileCount { get; set; }

    /// <summary>
    /// Gets or sets the total size in bytes of files that would be removed.
    /// </summary>
    public long TotalBytes { get; set; }

    /// <summary>
    /// Gets or sets the manifest IDs that would be affected.
    /// </summary>
    public List<string> ManifestIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the human-readable names of manifests that would be affected.
    /// </summary>
    public List<string> ManifestNames { get; set; } = [];

    /// <summary>
    /// Gets a value indicating whether there are files to remove.
    /// </summary>
    public bool HasFilesToRemove => FileCount > 0;
}
