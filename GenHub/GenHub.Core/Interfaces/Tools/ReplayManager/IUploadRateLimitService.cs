using GenHub.Core.Constants;
using GenHub.Core.Models.Tools.ReplayManager;

namespace GenHub.Core.Interfaces.Tools.ReplayManager;

/// <summary>
/// Manages upload rate limiting to prevent spam.
/// </summary>
public interface IUploadRateLimitService
{
    /// <summary>
    /// Maximum upload size per week in bytes (10 MB).
    /// </summary>
    public const long MaxWeeklyUploadBytes = ReplayManagerConstants.MaxWeeklyUploadBytes;

    /// <summary>
    /// Checks if an upload of the specified size is allowed.
    /// </summary>
    /// <param name="fileSizeBytes">The size of the file to upload in bytes.</param>
    /// <returns>True if the upload is allowed, false if it would exceed the weekly limit.</returns>
    Task<bool> CanUploadAsync(long fileSizeBytes);

    /// <summary>
    /// Records a successful upload.
    /// </summary>
    /// <param name="fileSizeBytes">The size of the uploaded file in bytes.</param>
    /// <param name="url">The public URL of the uploaded file.</param>
    /// <param name="fileName">The name of the uploaded file.</param>
    void RecordUpload(long fileSizeBytes, string url, string fileName);

    /// <summary>
    /// Gets the current usage information.
    /// </summary>
    /// <returns>A <see cref="UsageInfo"/> containing the current usage information.</returns>
    Task<UsageInfo> GetUsageInfoAsync();

    /// <summary>
    /// Gets the history of uploads.
    /// </summary>
    /// <returns>A list of upload history items.</returns>
    Task<IEnumerable<UploadHistoryItem>> GetUploadHistoryAsync();

    /// <summary>
    /// Removes a specific upload history item by URL.
    /// </summary>
    /// <param name="url">The URL of the upload to remove.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task RemoveHistoryItemAsync(string url);

    /// <summary>
    /// Clears all upload history.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ClearHistoryAsync();
}
