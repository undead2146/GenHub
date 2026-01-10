using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Models.AppUpdate;
using GenHub.Core.Models.GitHub;
using GenHub.Features.AppUpdate.Interfaces;
using Velopack;

namespace GenHub.Features.Info.Services;

/// <summary>
/// Mock implementation of IVelopackUpdateManager for interactive demos.
/// </summary>
public class MockVelopackUpdateManager : IVelopackUpdateManager
{
    /// <inheritdoc/>
    public bool HasUpdateAvailableFromGitHub => true;

    /// <inheritdoc/>
    public string? LatestVersionFromGitHub => "0.0.5";

    /// <inheritdoc/>
    public bool IsUpdatePendingRestart => false;

    /// <inheritdoc/>
    public bool HasArtifactUpdateAvailable => false;

    /// <inheritdoc/>
    public ArtifactUpdateInfo? LatestArtifactUpdate => null;

    /// <inheritdoc/>
    public int? SubscribedPrNumber { get; set; }

    /// <inheritdoc/>
    public string? SubscribedBranch { get; set; }

    /// <inheritdoc/>
    public bool IsPrMergedOrClosed => false;

    /// <inheritdoc/>
    public void ApplyUpdatesAndExit(UpdateInfo updateInfo)
    {
    }

    /// <inheritdoc/>
    public void ApplyUpdatesAndRestart(UpdateInfo updateInfo)
    {
    }

    /// <inheritdoc/>
    public Task<ArtifactUpdateInfo?> CheckForArtifactUpdatesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<ArtifactUpdateInfo?>(null);

    /// <inheritdoc/>
    public Task<UpdateInfo?> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        // Return null to simulate "check completed but no Velopack update found"
        // We will manually set state in the ViewModel
        return Task.FromResult<UpdateInfo?>(null);
    }

    /// <inheritdoc/>
    public void ClearCache()
    {
    }

    /// <inheritdoc/>
    public Task DownloadUpdatesAsync(UpdateInfo updateInfo, IProgress<UpdateProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        // Simulate download
        _ = Task.Run(
            async () =>
            {
                for (int i = 0; i <= 100; i += 10)
                {
                    progress?.Report(new UpdateProgress { PercentComplete = i, Status = "Downloading demo update..." });
                    await Task.Delay(200, cancellationToken);
                }
            },
            cancellationToken);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<ArtifactUpdateInfo>> GetArtifactsForBranchAsync(string branchName, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ArtifactUpdateInfo>>([]);

    /// <inheritdoc/>
    public Task<IReadOnlyList<ArtifactUpdateInfo>> GetArtifactsForPullRequestAsync(int prNumber, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ArtifactUpdateInfo>>([]);

    /// <inheritdoc/>
    public Task<IReadOnlyList<string>> GetBranchesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<string>>([]);

    /// <inheritdoc/>
    public Task<IReadOnlyList<PullRequestInfo>> GetOpenPullRequestsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PullRequestInfo>>([]);

    /// <inheritdoc/>
    public Task InstallArtifactAsync(ArtifactUpdateInfo artifactInfo, IProgress<UpdateProgress>? progress = null, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public Task InstallPrArtifactAsync(PullRequestInfo prInfo, IProgress<UpdateProgress>? progress = null, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public void Uninstall()
    {
    }
}
