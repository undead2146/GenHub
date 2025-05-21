using System;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Models;
using GenHub.Core.Models.GameProfiles;
using GenHub.Core.Models.Results;
using GenHub.Core.Models.GitHub;

namespace GenHub.Core.Interfaces.GitHub
{
    /// <summary>
    /// Service for installing GitHub artifacts as game versions
    /// </summary>
    public interface IGitHubArtifactInstaller
    {
        /// <summary>
        /// Downloads and installs an artifact as a game version
        /// </summary>
        Task<OperationResult<GameVersion>> InstallArtifactAsync(
            GitHubArtifact artifact,
            IProgress<InstallProgress>? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if an artifact is already installed
        /// </summary>
        Task<bool> IsArtifactInstalledAsync(
            GitHubArtifact artifact,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates a suitable installation name for an artifact
        /// </summary>
        string GenerateInstallName(GitHubArtifact artifact);

        Task<OperationResult<GameVersion>> InstallReleaseAssetAsync(
        string assetDownloadUrl, // URL to download the asset
        string assetName,        // Name of the asset (for naming the installed version)
        GitHubRepoSettings repoSettings, // For context, creating install path
        // Potentially pass the full GitHubRelease and GitHubReleaseAsset models for more metadata
        IProgress<InstallProgress> progress,
        CancellationToken cancellationToken);

        Task<OperationResult<GameVersion>> InstallVersionFromReleaseAssetAsync(
        GitHubRelease release, // Full release model
        GitHubReleaseAsset asset, // Full asset model
        string downloadedFilePath, // Path to the already downloaded asset
        ExtractOptions? options,
        IProgress<InstallProgress>? progress,
        CancellationToken cancellationToken);
    }
}
