namespace GenHub.Core.Constants;

/// <summary>
/// Constants for the Replay Manager feature.
/// </summary>
public static class ReplayManagerConstants
{
    /// <summary>
    /// Maximum size for a single replay file in bytes (1 MB).
    /// </summary>
    public const long MaxReplaySizeBytes = 1024 * 1024;

    /// <summary>
    /// Maximum upload bytes per period (10 MB).
    /// </summary>
    public const long MaxUploadBytesPerPeriod = 10 * 1024 * 1024;

    /// <summary>
    /// Prefix for temporary import files.
    /// </summary>
    public const string TempImportFilePrefix = "genhub_import_";

    /// <summary>
    /// Prefix for temporary share files.
    /// </summary>
    public const string TempShareFilePrefix = "genhub_share_";

    /// <summary>
    /// Default file name for imported replays.
    /// </summary>
    public const string DefaultImportedReplayFileName = "imported_replay.rep";

    /// <summary>
    /// Standard extension for replay files.
    /// </summary>
    public const string ReplayExtension = ".rep";

    /// <summary>
    /// Standard extension for ZIP archives.
    /// </summary>
    public const string ZipExtension = ".zip";

    /// <summary>
    /// Number of days files are retained in cloud storage.
    /// </summary>
    public const int RetentionDays = 14;
}