using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using GenHub.Core.Interfaces.Content;
using GenHub.Core.Interfaces.Notifications;
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

        public object Lock { get; } = new();

        public void Dispose()
        {
            InternalCts.Dispose();
        }
    }

    private readonly ConcurrentDictionary<string, InFlightDownload> _inFlightDownloads = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public async Task<OperationResult<ContentManifest>> DownloadContentAsync(
        ContentSearchResult searchResult,
        IProgress<ContentAcquisitionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(searchResult);

        var key = !string.IsNullOrWhiteSpace(searchResult.Id)
            ? $"{searchResult.ProviderName}::{searchResult.Id}"
            : $"{searchResult.ProviderName}::{searchResult.Name}";

        InFlightDownload inFlight;
        bool isInitiator = false;

        lock (_inFlightDownloads)
        {
            if (!_inFlightDownloads.TryGetValue(key, out inFlight!))
            {
                inFlight = new InFlightDownload();
                isInitiator = true;
                _inFlightDownloads[key] = inFlight;
            }

            lock (inFlight.Lock)
            {
                inFlight.WaiterCount++;
            }
        }

        Action<ContentAcquisitionProgress>? callback = progress != null ? progress.Report : null;
        if (callback != null)
        {
            lock (inFlight.Lock)
            {
                inFlight.ProgressCallbacks += callback;
            }
        }

        if (isInitiator)
        {
            var multiplexedProgress = new Progress<ContentAcquisitionProgress>(p =>
            {
                Action<ContentAcquisitionProgress>? callbacks;
                lock (inFlight.Lock)
                {
                    callbacks = inFlight.ProgressCallbacks;
                }

                callbacks?.Invoke(p);
            });

            _ = StartDownloadTaskAsync(inFlight, searchResult, key, multiplexedProgress);
        }

        var unregistered = 0;
        void OnCallerCancelled()
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

        var reg = cancellationToken.CanBeCanceled ? cancellationToken.Register(OnCallerCancelled) : default;

        try
        {
            return await inFlight.Task.WaitAsync(cancellationToken);
        }
        finally
        {
            reg.Dispose();
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

            if (callback != null)
            {
                lock (inFlight.Lock)
                {
                    inFlight.ProgressCallbacks -= callback;
                }
            }
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
            var result = await ExecuteDownloadAsync(searchResult, key, progress, inFlight.InternalCts.Token);
            inFlight.Tcs.TrySetResult(result);
        }
        catch (OperationCanceledException oce)
        {
            inFlight.Tcs.TrySetCanceled(oce.CancellationToken);
        }
        catch (Exception ex)
        {
            inFlight.Tcs.TrySetException(ex);
        }
        finally
        {
            lock (_inFlightDownloads)
            {
                _inFlightDownloads.TryRemove(key, out _);
            }

            inFlight.Dispose();
        }
    }

    private async Task<OperationResult<ContentManifest>> ExecuteDownloadAsync(
        ContentSearchResult searchResult,
        string inFlightKey,
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
