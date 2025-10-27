namespace GenHub.Core.Models.GeneralsOnline;

/// <summary>
/// Represents a Generals Online release with version information and download URLs.
/// </summary>
public class GeneralsOnlineRelease
{
    /// <summary>
    /// Gets version string in format: MMDDYY_QFE# (e.g., "101525_QFE5").
    /// </summary>
    required public string Version { get; init; }

    /// <summary>
    /// Gets date encoded in version (e.g., 101525 = October 15, 2025).
    /// </summary>
    public DateTime VersionDate { get; init; }

    /// <summary>
    /// Gets actual release date.
    /// </summary>
    public DateTime ReleaseDate { get; init; }

    /// <summary>
    /// Gets URL to installer EXE.
    /// </summary>
    required public string InstallerUrl { get; init; }

    /// <summary>
    /// Gets URL to portable ZIP.
    /// </summary>
    required public string PortableUrl { get; init; }

    /// <summary>
    /// Gets size of installer in bytes.
    /// </summary>
    public long InstallerSize { get; init; }

    /// <summary>
    /// Gets size of portable ZIP in bytes.
    /// </summary>
    public long PortableSize { get; init; }

    /// <summary>
    /// Gets release changelog/notes.
    /// </summary>
    public string? Changelog { get; init; }
}
