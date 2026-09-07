using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Interfaces.Content;
using GenHub.Core.Interfaces.Notifications;
using GenHub.Core.Models.Content;
using GenHub.Core.Models.Manifest;
using GenHub.Core.Models.Results;
using GenHub.Core.Models.Results.Content;
using GenHub.Features.Downloads.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GenHub.Tests.Core.Features.Downloads.Services;

/// <summary>
/// Unit tests for <see cref="ContentDownloadCoordinator"/>.
/// </summary>
public sealed class ContentDownloadCoordinatorTests
{
    /// <summary>
    /// Verifies that concurrent calls to download the exact same content are deduplicated to a single acquisition task.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task DownloadContentAsync_ConcurrentCallsSameContent_DeduplicatesToSingleAcquisitionAsync()
    {
        // Arrange
        var orchestratorMock = new Mock<IContentOrchestrator>();
        var stateServiceMock = new Mock<IContentStateService>();
        var notificationServiceMock = new Mock<INotificationService>();

        var testManifest = new ContentManifest
        {
            Id = ManifestId.Create("1.185.moddb.mod.rotr"),
            Name = "Rise of the Reds",
        };

        var acquisitionTcs = new TaskCompletionSource<OperationResult<ContentManifest>>();

        orchestratorMock
            .Setup(x => x.AcquireContentAsync(It.IsAny<ContentSearchResult>(), It.IsAny<IProgress<ContentAcquisitionProgress>>(), It.IsAny<CancellationToken>()))
            .Returns(acquisitionTcs.Task);

        var coordinator = new ContentDownloadCoordinator(
            orchestratorMock.Object,
            stateServiceMock.Object,
            notificationServiceMock.Object,
            NullLogger<ContentDownloadCoordinator>.Instance);

        var searchResult1 = new ContentSearchResult
        {
            Id = "moddb_rotr_185",
            Name = "Rise of the Reds",
            ProviderName = "ModDB",
        };

        var searchResult2 = new ContentSearchResult
        {
            Id = "moddb_rotr_185",
            Name = "Rise of the Reds",
            ProviderName = "ModDB",
        };

        // Act: Start two downloads concurrently
        var task1 = coordinator.DownloadContentAsync(searchResult1);
        var task2 = coordinator.DownloadContentAsync(searchResult2);

        // Complete the single in-flight acquisition
        acquisitionTcs.SetResult(OperationResult<ContentManifest>.CreateSuccess(testManifest));

        var result1 = await task1;
        var result2 = await task2;

        // Assert
        Assert.True(result1.Success);
        Assert.True(result2.Success);
        Assert.Same(result1.Data, result2.Data);

        // Verify AcquireContentAsync was only invoked once
        orchestratorMock.Verify(
            x => x.AcquireContentAsync(It.IsAny<ContentSearchResult>(), It.IsAny<IProgress<ContentAcquisitionProgress>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Verifies that when a second caller cancels its own token, it unblocks immediately without cancelling the first caller's download.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task DownloadContentAsync_SecondCallerCancels_DoesNotBlockOrCancelFirstCallerAsync()
    {
        // Arrange
        var orchestratorMock = new Mock<IContentOrchestrator>();
        var stateServiceMock = new Mock<IContentStateService>();
        var notificationServiceMock = new Mock<INotificationService>();

        var testManifest = new ContentManifest
        {
            Id = ManifestId.Create("1.0.moddb.mod.test"),
            Name = "Test Mod",
        };

        var acquisitionTcs = new TaskCompletionSource<OperationResult<ContentManifest>>();

        orchestratorMock
            .Setup(x => x.AcquireContentAsync(It.IsAny<ContentSearchResult>(), It.IsAny<IProgress<ContentAcquisitionProgress>>(), It.IsAny<CancellationToken>()))
            .Returns(acquisitionTcs.Task);

        var coordinator = new ContentDownloadCoordinator(
            orchestratorMock.Object,
            stateServiceMock.Object,
            notificationServiceMock.Object,
            NullLogger<ContentDownloadCoordinator>.Instance);

        var searchResult = new ContentSearchResult
        {
            Id = "test_item_1",
            Name = "Test Mod",
            ProviderName = "ModDB",
        };

        using var cts1 = new CancellationTokenSource();
        using var cts2 = new CancellationTokenSource();

        // Caller 1 starts
        var task1 = coordinator.DownloadContentAsync(searchResult, cancellationToken: cts1.Token);

        // Caller 2 joins
        var task2 = coordinator.DownloadContentAsync(searchResult, cancellationToken: cts2.Token);

        // Caller 2 cancels its token
        cts2.Cancel();

        // Caller 2 should throw OperationCanceledException immediately
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task2);

        // Caller 1 is still in flight
        Assert.False(task1.IsCompleted);

        // Now complete download for caller 1
        acquisitionTcs.SetResult(OperationResult<ContentManifest>.CreateSuccess(testManifest));
        var result1 = await task1;

        Assert.True(result1.Success);
        Assert.Same(testManifest, result1.Data);
    }

    /// <summary>
    /// Verifies that progress is multiplexed to both the initial caller and subsequent concurrent callers.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task DownloadContentAsync_ConcurrentCallers_MultiplexesProgressToBothAsync()
    {
        // Arrange
        var orchestratorMock = new Mock<IContentOrchestrator>();
        var stateServiceMock = new Mock<IContentStateService>();
        var notificationServiceMock = new Mock<INotificationService>();

        var testManifest = new ContentManifest
        {
            Id = ManifestId.Create("1.0.moddb.mod.test"),
            Name = "Test Mod",
        };

        IProgress<ContentAcquisitionProgress>? capturedProgress = null;
        var acquisitionTcs = new TaskCompletionSource<OperationResult<ContentManifest>>();

        orchestratorMock
            .Setup(x => x.AcquireContentAsync(It.IsAny<ContentSearchResult>(), It.IsAny<IProgress<ContentAcquisitionProgress>>(), It.IsAny<CancellationToken>()))
            .Callback<ContentSearchResult, IProgress<ContentAcquisitionProgress>?, CancellationToken>((_, p, _) => capturedProgress = p)
            .Returns(acquisitionTcs.Task);

        var coordinator = new ContentDownloadCoordinator(
            orchestratorMock.Object,
            stateServiceMock.Object,
            notificationServiceMock.Object,
            NullLogger<ContentDownloadCoordinator>.Instance);

        var searchResult = new ContentSearchResult
        {
            Id = "test_item_2",
            Name = "Test Mod 2",
            ProviderName = "ModDB",
        };

        var progressList1 = new List<double>();
        var progressList2 = new List<double>();

        var p1 = new Progress<ContentAcquisitionProgress>(p => progressList1.Add(p.ProgressPercentage));
        var p2 = new Progress<ContentAcquisitionProgress>(p => progressList2.Add(p.ProgressPercentage));

        var task1 = coordinator.DownloadContentAsync(searchResult, p1);
        var task2 = coordinator.DownloadContentAsync(searchResult, p2);

        Assert.NotNull(capturedProgress);
        capturedProgress.Report(new ContentAcquisitionProgress { ProgressPercentage = 42 });

        // Allow any async dispatcher/post if needed
        await Task.Delay(50);

        acquisitionTcs.SetResult(OperationResult<ContentManifest>.CreateSuccess(testManifest));
        await Task.WhenAll(task1, task2);

        Assert.Contains(42, progressList1);
        Assert.Contains(42, progressList2);
    }
}
