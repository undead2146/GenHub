namespace GenHub.Core.Messages;

/// <summary>
/// Message sent when download settings have changed.
/// </summary>
/// <param name="MaxConcurrentDownloads">Maximum number of concurrent downloads.</param>
/// <param name="BufferSizeKB">Download buffer size in kilobytes.</param>
/// <param name="TimeoutSeconds">Download timeout in seconds.</param>
/// <param name="UserAgent">User agent string for downloads.</param>
public record DownloadSettingsChangedMessage(int MaxConcurrentDownloads, double BufferSizeKB, int TimeoutSeconds, string UserAgent);
