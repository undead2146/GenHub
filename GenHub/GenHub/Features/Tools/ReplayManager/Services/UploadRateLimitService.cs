using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using GenHub.Core.Interfaces.Common;
using GenHub.Core.Interfaces.Tools.ReplayManager;
using GenHub.Core.Models.Tools.ReplayManager;
using Microsoft.Extensions.Logging;

namespace GenHub.Features.Tools.ReplayManager.Services;

/// <summary>
/// Implementation of <see cref="IUploadRateLimitService"/> for tracking upload quotas.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="UploadRateLimitService"/> class.
/// </remarks>
/// <param name="logger">Logger instance.</param>
/// <param name="appConfig">Application configuration service.</param>
public sealed class UploadRateLimitService(ILogger<UploadRateLimitService> logger, IAppConfiguration appConfig) : IUploadRateLimitService
{
    /// <summary>
    /// Record of an upload for rate limiting purposes.
    /// </summary>
    internal sealed class UploadRecord
    {
        /// <summary>
        /// Gets or sets the timestamp of the upload.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the size of the upload in bytes.
        /// </summary>
        public long SizeBytes { get; set; }

        /// <summary>
        /// Gets or sets the public URL of the upload.
        /// </summary>
        public string? Url { get; set; }

        /// <summary>
        /// Gets or sets the name of the uploaded file.
        /// </summary>
        public string? FileName { get; set; }
    }

    private static readonly object _fileLock = new();
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly ILogger<UploadRateLimitService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly string _historyFilePath = Path.Combine(appConfig.GetConfiguredDataPath(), "upload_history.json");
    private List<UploadRecord>? _cache;

    /// <inheritdoc />
    public async Task<bool> CanUploadAsync(long fileSizeBytes)
    {
        var usage = await GetUsageInfoAsync();
        return usage.UsedBytes + fileSizeBytes <= usage.LimitBytes;
    }

    /// <inheritdoc />
    public void RecordUpload(long fileSizeBytes, string url, string fileName)
    {
        lock (_fileLock)
        {
            try
            {
                var history = LoadHistoryInternal();
                history.Add(new UploadRecord
                {
                    Timestamp = DateTime.UtcNow,
                    SizeBytes = fileSizeBytes,
                    Url = url,
                    FileName = fileName,
                });

                SaveHistoryInternal(history);
                _cache = history; // Update cache
                _logger.LogInformation("Recorded upload of {Size} bytes. Total history: {Count} items.", fileSizeBytes, history.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to record upload");
            }
        }
    }

    /// <inheritdoc />
    public Task<UsageInfo> GetUsageInfoAsync()
    {
        var history = LoadHistoryInternal();
        var weekAgo = DateTime.UtcNow.AddDays(-7);

        // Calculate usage from the last 7 days
        var recentUploads = history.Where(r => r.Timestamp >= weekAgo).ToList();
        var usedBytes = recentUploads.Sum(r => r.SizeBytes);

        // Reset date is 7 days from the oldest upload in the current rolling week
        var oldestInWindow = recentUploads.OrderBy(r => r.Timestamp).FirstOrDefault();
        var resetDate = oldestInWindow != null
            ? oldestInWindow.Timestamp.AddDays(7)
            : DateTime.UtcNow;

        return Task.FromResult(new UsageInfo(usedBytes, IUploadRateLimitService.MaxWeeklyUploadBytes, resetDate));
    }

    /// <inheritdoc />
    public Task<IEnumerable<UploadHistoryItem>> GetUploadHistoryAsync()
    {
        var history = LoadHistoryInternal();

        // Return all history (up to 30 days as filtered in loading)
        var items = history.Select(r => new UploadHistoryItem(
            r.Timestamp,
            r.SizeBytes,
            r.Url ?? string.Empty,
            r.FileName ?? "Unknown File"));

        return Task.FromResult(items);
    }

    /// <inheritdoc />
    public Task RemoveHistoryItemAsync(string url)
    {
        return Task.Run(() =>
        {
            lock (_fileLock)
            {
                try
                {
                    var history = LoadHistoryInternal();
                    var removed = history.RemoveAll(r => r.Url == url);

                    if (removed > 0)
                    {
                        SaveHistoryInternal(history);
                        _cache = history; // Update cache
                        _logger.LogInformation("Removed {Count} upload history item(s) with URL: {Url}", removed, url);
                    }
                    else
                    {
                        _logger.LogWarning("No upload history item found with URL: {Url}", url);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to remove upload history item with URL: {Url}", url);
                }
            }
        });
    }

    /// <inheritdoc />
    public Task ClearHistoryAsync()
    {
        return Task.Run(() =>
        {
            lock (_fileLock)
            {
                try
                {
                    _cache = [];

                    if (File.Exists(_historyFilePath))
                    {
                        File.Delete(_historyFilePath);
                        _logger.LogInformation("Cleared all upload history");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to clear upload history");
                }
            }
        });
    }

    private List<UploadRecord> LoadHistoryInternal()
    {
        lock (_fileLock)
        {
            if (_cache != null)
            {
                return [.. _cache];
            }

            try
            {
                if (!File.Exists(_historyFilePath))
                {
                    _logger.LogDebug("Upload history file not found at {Path}, starting fresh.", _historyFilePath);
                    _cache = [];
                    return [];
                }

                var json = File.ReadAllText(_historyFilePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    _cache = [];
                    return [];
                }

                var history = JsonSerializer.Deserialize<List<UploadRecord>>(json, _jsonOptions) ?? [];

                // Clean up old entries (older than 30 days) to keep file size small
                var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
                _cache = [.. history.Where(r => r.Timestamp >= thirtyDaysAgo).OrderByDescending(r => r.Timestamp)];

                _logger.LogDebug("Loaded {Count} upload records from history.", _cache.Count);
                return [.. _cache];
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load upload history from {Path}. The file may be corrupt or inaccessible.", _historyFilePath);

                // Return empty but don't set cache to empty if we want to retry next time?
                // Actually, if it's corrupt, returning empty might cause us to overwrite it with new data.
                // We'll return a new list so the app continues working.
                return [];
            }
        }
    }

    private void SaveHistoryInternal(List<UploadRecord> history)
    {
        lock (_fileLock)
        {
            try
            {
                var directory = Path.GetDirectoryName(_historyFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(history, _jsonOptions);
                File.WriteAllText(_historyFilePath, json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save upload history to {Path}", _historyFilePath);
            }
        }
    }
}
