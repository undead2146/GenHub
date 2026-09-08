using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using GenHub.Core.Extensions;
using GenHub.Core.Interfaces.Content;
using GenHub.Core.Interfaces.Notifications;
using GenHub.Core.Messages;
using GenHub.Core.Models.Content;
using GenHub.Core.Models.Enums;
using GenHub.Core.Models.Manifest;
using GenHub.Core.Models.Results;
using GenHub.Core.Models.Results.Content;
using Microsoft.Extensions.Logging;

namespace GenHub.Features.Downloads.Services;

/// <summary>
/// Implementation of the content download coordinator.
/// </summary>
public sealed class ContentDownloadCoordinator(
    IContentOrchestrator contentOrchestrator,
    IContentStateService contentStateService,
    INotificationService notificationService,
    ILogger<ContentDownloadCoordinator> logger) : IContentDownloadCoordinator
{
    private sealed class InFlightDownload : IDisposable
    {
        public TaskCompletionSource<OperationResult<ContentManifest>> Tcs { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<OperationResult<ContentManifest>> Task => Tcs.Task;

        public CancellationTokenSource InternalCts { get; } = new();

        public int WaiterCount { get; set; }

        public Action<ContentAcquisitionProgress>? ProgressCallbacks { get; set; }

        public double LastProgressPercentage { get; set; }

        public string LastStatusMessage { get; set; } = string.Empty;

        public object Lock { get; } = new();

        public void Dispose()
        {
            InternalCts.Dispose();
        }
    }

    private readonly ConcurrentDictionary<string, InFlightDownload> _inFlightDownloads = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Computes the download key for a content item.
    /// </summary>
    /// <param name="searchResult">The search result.</param>
    /// <returns>A string key uniquely identifying the content download.</returns>
    public static string GetDownloadKey(ContentSearchResult searchResult)
    {
        ArgumentNullException.ThrowIfNull(searchResult);

        return !string.IsNullOrWhiteSpace(searchResult.Id)
            ? $"{searchResult.ProviderName}::{searchResult.Id}"
            : $"{searchResult.ProviderName}::{searchResult.Name}";
    }

    /// <inheritdoc />
    public bool IsDownloading(ContentSearchResult searchResult)
    {
        if (searchResult == null)
        {
            return false;
        }

        var key = GetDownloadKey(searchResult);
        if (_inFlightDownloads.TryGetValue(key, out var inFlight) &&
            !inFlight.InternalCts.IsCancellationRequested &&
            !inFlight.Task.IsCompleted)
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(searchResult.Name))
        {
            var nameKey = $"{searchResult.ProviderName}::{searchResult.Name}";
            if (_inFlightDownloads.TryGetValue(nameKey, out inFlight) &&
                !inFlight.InternalCts.IsCancellationRequested &&
                !inFlight.Task.IsCompleted)
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc />
    public bool TryGetDownloadProgress(ContentSearchResult searchResult, out double progressPercentage, out string statusMessage)
    {
        progressPercentage = 0;
        statusMessage = string.Empty;

        if (searchResult == null)
        {
            return false;
        }

        var key = GetDownloadKey(searchResult);
        if (!_inFlightDownloads.TryGetValue(key, out var inFlight) && !string.IsNullOrWhiteSpace(searchResult.Name))
        {
            var nameKey = $"{searchResult.ProviderName}::{searchResult.Name}";
            _inFlightDownloads.TryGetValue(nameKey, out inFlight);
        }

        if (inFlight != null && !inFlight.Task.IsCompleted)
        {
            lock (inFlight.Lock)
            {
                progressPercentage = inFlight.LastProgressPercentage;
                statusMessage = inFlight.LastStatusMessage;
            }

            return true;
        }

        return false;
    }

    /// <inheritdoc />
    public async Task<OperationResult<ContentManifest>> DownloadContentAsync(
        ContentSearchResult searchResult,
        IProgress<ContentAcquisitionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(searchResult);

        var key = GetDownloadKey(searchResult);

        var inFlight = GetOrCreateInFlightDownload(key, out var isInitiator);

        Action<ContentAcquisitionProgress>? callback = progress != null ? progress.Report : null;
        AttachProgressCallback(inFlight, callback);

        if (isInitiator)
        {
            var multiplexedProgress = new Progress<ContentAcquisitionProgress>(p =>
            {
                var status = p.FormatProgressStatus();
                lock (inFlight.Lock)
                {
                    inFlight.LastProgressPercentage = p.ProgressPercentage;
                    inFlight.LastStatusMessage = status;
                }

                Action<ContentAcquisitionProgress>? callbacks;
                lock (inFlight.Lock)
                {
                    callbacks = inFlight.ProgressCallbacks;
                }

                callbacks?.Invoke(p);

                try
                {
                    WeakReferenceMessenger.Default.Send(new ContentDownloadProgressMessage(
                        key,
                        searchResult.Id,
                        searchResult.ProviderName,
                        searchResult.Name,
                        p.ProgressPercentage,
                        status));
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to broadcast ContentDownloadProgressMessage for {Key}", key);
                }
            });

            _ = StartDownloadTaskAsync(inFlight, searchResult, key, multiplexedProgress);
        }

        var unregistered = 0;
        var reg = default(CancellationTokenRegistration);

        try
        {
            if (cancellationToken.CanBeCanceled)
            {
                reg = cancellationToken.Register(() => DecrementWaiterAndCancelIfEmpty(inFlight, ref unregistered));
            }

            return await inFlight.Task.WaitAsync(cancellationToken);
        }
        finally
        {
            await reg.DisposeAsync();
            DecrementWaiterAndCancelIfEmpty(inFlight, ref unregistered);
            DetachProgressCallback(inFlight, callback);
        }
    }

    private static void DecrementWaiterAndCancelIfEmpty(InFlightDownload inFlight, ref int unregistered)
    {
        if (Interlocked.Exchange(ref unregistered, 1) == 0)
        {
            lock (inFlight.Lock)
            {
                inFlight.WaiterCount--;
                if (inFlight.WaiterCount <= 0)
                {
                    try
                    {
                        inFlight.InternalCts.Cancel();
                    }
                    catch (ObjectDisposedException)
                    {
                        // Ignored if already disposed
                    }
                }
            }
        }
    }

    private static void AttachProgressCallback(InFlightDownload inFlight, Action<ContentAcquisitionProgress>? callback)
    {
        if (callback == null)
        {
            return;
        }

        lock (inFlight.Lock)
        {
            inFlight.ProgressCallbacks += callback;
        }
    }

    private static void DetachProgressCallback(InFlightDownload inFlight, Action<ContentAcquisitionProgress>? callback)
    {
        if (callback == null)
        {
            return;
        }

        lock (inFlight.Lock)
        {
            inFlight.ProgressCallbacks -= callback;
        }
    }

    private InFlightDownload GetOrCreateInFlightDownload(string key, out bool isInitiator)
    {
        lock (_inFlightDownloads)
        {
            if (!_inFlightDownloads.TryGetValue(key, out var inFlight) ||
                inFlight.InternalCts.IsCancellationRequested ||
                inFlight.Task.IsCompleted)
            {
                inFlight = new InFlightDownload();
                isInitiator = true;
                _inFlightDownloads[key] = inFlight;
            }
            else
            {
                isInitiator = false;
            }

            lock (inFlight.Lock)
            {
                inFlight.WaiterCount++;
            }

            return inFlight;
        }
    }

    private async Task StartDownloadTaskAsync(
        InFlightDownload inFlight,
        ContentSearchResult searchResult,
        string key,
        IProgress<ContentAcquisitionProgress> progress)
    {
        try
        {
            WeakReferenceMessenger.Default.Send(new ContentDownloadStartedMessage(
                key,
                searchResult.Id,
                searchResult.ProviderName,
                searchResult.Name));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to broadcast ContentDownloadStartedMessage for {Key}", key);
        }

        var success = false;
        string? errorMessage = null;

        try
        {
            var result = await ExecuteDownloadAsync(searchResult, progress, inFlight.InternalCts.Token);
            success = result.Success;
            errorMessage = result.FirstError;
            inFlight.Tcs.TrySetResult(result);
        }
        catch (OperationCanceledException oce)
        {
            errorMessage = "Download cancelled";
            inFlight.Tcs.TrySetCanceled(oce.CancellationToken);
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            inFlight.Tcs.TrySetException(ex);
        }
        finally
        {
            lock (_inFlightDownloads)
            {
                if (_inFlightDownloads.TryGetValue(key, out var current) && ReferenceEquals(current, inFlight))
                {
                    _inFlightDownloads.TryRemove(key, out _);
                }
            }

            try
            {
                WeakReferenceMessenger.Default.Send(new ContentDownloadCompletedMessage(
                    key,
                    searchResult.Id,
                    searchResult.ProviderName,
                    searchResult.Name,
                    success,
                    errorMessage));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to broadcast ContentDownloadCompletedMessage for {Key}", key);
            }

            inFlight.Dispose();
        }
    }

    private async Task<OperationResult<ContentManifest>> ExecuteDownloadAsync(
        ContentSearchResult searchResult,
        IProgress<ContentAcquisitionProgress>? progress,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Starting download for content: {Name} ({Provider})", searchResult.Name, searchResult.ProviderName);

            var result = await contentOrchestrator.AcquireContentAsync(searchResult, progress, cancellationToken);

            if (result.Success && result.Data != null)
            {
                var manifest = result.Data;
                logger.LogInformation("Successfully downloaded and stored content: {ManifestId}", manifest.Id.Value);

                // Remember the pre-download catalog ID, then point the search result at the stored
                // manifest so Add to Profile and later state lookups use the real manifest ID
                // (parity with the grid download path in DownloadsBrowserViewModel).
                var originalContentId = searchResult.Id ?? string.Empty;
                searchResult.UpdateId(manifest.Id.Value);

                // Update state. The event carries both the original catalog ID and the manifest ID
                // so every subscriber can match regardless of which ID it currently holds.
                contentStateService.NotifyStateChanged(originalContentId, ContentState.Downloaded, manifest.Id.Value);

                // Notify other components
                try
                {
                    WeakReferenceMessenger.Default.Send(new ContentAcquiredMessage(manifest));
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to send ContentAcquiredMessage for {ManifestId}", manifest.Id.Value);
                }

                notificationService.ShowSuccess("Download Complete", $"Downloaded {searchResult.Name}");

                return OperationResult<ContentManifest>.CreateSuccess(manifest);
            }

            var errorMsg = result.FirstError ?? "Unknown error";
            logger.LogError("Failed to download {ItemName}: {Error}", searchResult.Name, errorMsg);

            return OperationResult<ContentManifest>.CreateFailure(errorMsg);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException ex)
        {
            logger.LogInformation(ex, "Download timed out or cancelled internally for: {Name}", searchResult.Name);
            return OperationResult<ContentManifest>.CreateFailure("Download cancelled");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error downloading content: {Name}", searchResult.Name);
            return OperationResult<ContentManifest>.CreateFailure($"An unexpected error occurred: {ex.Message}");
        }
    }
}
