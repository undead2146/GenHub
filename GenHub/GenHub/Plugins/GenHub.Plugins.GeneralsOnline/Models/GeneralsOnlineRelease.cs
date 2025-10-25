namespace GenHub.Plugins.GeneralsOnline.Models;

/// <summary>
/// Represents a Generals Online release with version information and download URLs.
/// </summary>
public class GeneralsOnlineRelease
{
    /// <summary>
    /// Version string in format: YYMMDD_QFE# (e.g., "101525_QFE5")
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// Date encoded in version (e.g., 101525 = October 15, 2025)
    /// </summary>
    public DateTime VersionDate { get; init; }

    /// <summary>
    /// Actual release date
    /// </summary>
    public DateTime ReleaseDate { get; init; }

    /// <summary>
    /// URL to installer EXE
    /// </summary>
    public required string InstallerUrl { get; init; }

    /// <summary>
    /// URL to portable ZIP
    /// </summary>
    public required string PortableUrl { get; init; }

    /// <summary>
    /// Size of installer in bytes
    /// </summary>
    public long InstallerSize { get; init; }

    /// <summary>
    /// Size of portable ZIP in bytes
    /// </summary>
    public long PortableSize { get; init; }

    /// <summary>
    /// Release changelog/notes
    /// </summary>
    public string? Changelog { get; init; }
}
