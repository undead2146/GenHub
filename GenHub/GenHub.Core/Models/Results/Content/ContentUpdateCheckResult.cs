namespace GenHub.Core.Models.Results.Content;

/// <summary>
/// Represents the result of a content update check operation.
/// </summary>
public class ContentUpdateCheckResult : ResultBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContentUpdateCheckResult"/> class.
    /// </summary>
    /// <param name="isUpdateAvailable">Whether an update is available.</param>
    /// <param name="latestVersion">The latest version available.</param>
    /// <param name="currentVersion">The currently installed version.</param>
    /// <param name="releaseDate">The release date of the latest version.</param>
    /// <param name="downloadUrl">The download URL for the update.</param>
    /// <param name="changelog">The changelog or release notes.</param>
    /// <param name="error">Error message if the check failed.</param>
    /// <param name="elapsed">Time taken for the operation.</param>
    private ContentUpdateCheckResult(
        bool isUpdateAvailable,
        string? latestVersion,
        string? currentVersion,
        DateTime? releaseDate = null,
        string? downloadUrl = null,
        string? changelog = null,
        string? error = null,
        TimeSpan elapsed = default)
        : base(error == null, error, elapsed)
    {
        IsUpdateAvailable = isUpdateAvailable;
        LatestVersion = latestVersion;
        CurrentVersion = currentVersion;
        ReleaseDate = releaseDate;
        DownloadUrl = downloadUrl;
        Changelog = changelog;
    }

    /// <summary>
    /// Gets a value indicating whether an update is available.
    /// </summary>
    public bool IsUpdateAvailable { get; }

    /// <summary>
    /// Gets the latest version available.
    /// </summary>
    public string? LatestVersion { get; }

    /// <summary>
    /// Gets the currently installed version.
    /// </summary>
    public string? CurrentVersion { get; }

    /// <summary>
    /// Gets the release date of the latest version.
    /// </summary>
    public DateTime? ReleaseDate { get; }

    /// <summary>
    /// Gets the download URL for the update.
    /// </summary>
    public string? DownloadUrl { get; }

    /// <summary>
    /// Gets the changelog or release notes for the latest version.
    /// </summary>
    public string? Changelog { get; }

    // -------------------------
    // Factory Methods
    // -------------------------

    /// <summary>
    /// Creates a successful result indicating an update is available.
    /// </summary>
    /// <param name="latestVersion">The latest version available.</param>
    /// <param name="currentVersion">The currently installed version.</param>
    /// <param name="releaseDate">The release date of the latest version.</param>
    /// <param name="downloadUrl">The download URL for the update.</param>
    /// <param name="changelog">The changelog or release notes.</param>
    /// <param name="elapsed">Time taken for the operation.</param>
    /// <returns>A <see cref="ContentUpdateCheckResult"/> indicating an update is available.</returns>
    public static ContentUpdateCheckResult CreateUpdateAvailable(
        string latestVersion,
        string? currentVersion = null,
        DateTime? releaseDate = null,
        string? downloadUrl = null,
        string? changelog = null,
        TimeSpan elapsed = default)
    {
        return new ContentUpdateCheckResult(
            isUpdateAvailable: true,
            latestVersion: latestVersion,
            currentVersion: currentVersion,
            releaseDate: releaseDate,
            downloadUrl: downloadUrl,
            changelog: changelog,
            elapsed: elapsed);
    }

    /// <summary>
    /// Creates a successful result indicating no update is available.
    /// </summary>
    /// <param name="currentVersion">The currently installed version.</param>
    /// <param name="latestVersion">The latest version checked (same as current).</param>
    /// <param name="elapsed">Time taken for the operation.</param>
    /// <returns>A <see cref="ContentUpdateCheckResult"/> indicating no update is available.</returns>
    public static ContentUpdateCheckResult CreateNoUpdateAvailable(
        string? currentVersion = null,
        string? latestVersion = null,
        TimeSpan elapsed = default)
    {
        return new ContentUpdateCheckResult(
            isUpdateAvailable: false,
            latestVersion: latestVersion ?? currentVersion,
            currentVersion: currentVersion,
            elapsed: elapsed);
    }

    /// <summary>
    /// Creates a failed result indicating the update check encountered an error.
    /// </summary>
    /// <param name="error">The error message.</param>
    /// <param name="currentVersion">The currently installed version, if known.</param>
    /// <param name="elapsed">Time taken for the operation.</param>
    /// <returns>A <see cref="ContentUpdateCheckResult"/> indicating the check failed.</returns>
    public static ContentUpdateCheckResult CreateFailure(
        string error,
        string? currentVersion = null,
        TimeSpan elapsed = default)
    {
        return new ContentUpdateCheckResult(
            isUpdateAvailable: false,
            latestVersion: null,
            currentVersion: currentVersion,
            error: error,
            elapsed: elapsed);
    }

    /// <summary>
    /// Creates a successful result for when no content is currently installed.
    /// </summary>
    /// <param name="latestVersion">The latest version available.</param>
    /// <param name="releaseDate">The release date of the latest version.</param>
    /// <param name="downloadUrl">The download URL.</param>
    /// <param name="changelog">The changelog or release notes.</param>
    /// <param name="elapsed">Time taken for the operation.</param>
    /// <returns>A <see cref="ContentUpdateCheckResult"/> indicating content is available for first-time install.</returns>
    public static ContentUpdateCheckResult CreateContentAvailable(
        string latestVersion,
        DateTime? releaseDate = null,
        string? downloadUrl = null,
        string? changelog = null,
        TimeSpan elapsed = default)
    {
        return new ContentUpdateCheckResult(
            isUpdateAvailable: true,
            latestVersion: latestVersion,
            currentVersion: null,
            releaseDate: releaseDate,
            downloadUrl: downloadUrl,
            changelog: changelog,
            elapsed: elapsed);
    }
}
