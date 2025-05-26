using System;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Models;
using GenHub.Core.Models.GameProfiles;
using GenHub.Core.Models.GitHub;
using GenHub.Core.Models.Results;

namespace GenHub.Core.Interfaces.GameVersions
{
    /// <summary>
    /// Interface for installing and uninstalling game versions
    /// </summary>
    public interface IGameVersionInstaller
    {
        /// <summary>
        /// Installs a game version from a GitHub artifact
        /// </summary>
        Task<OperationResult<GameVersion>> InstallVersionAsync(
            GitHubArtifact artifact,
            string zipPath,
            ExtractOptions? options = null,
            IProgress<InstallProgress>? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Installs a game version from a local ZIP file
        /// </summary>
        Task<OperationResult<GameVersion>> InstallVersionFromZipAsync(
            string zipPath,
            ExtractOptions? options = null,
            IProgress<InstallProgress>? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Installs a game version from an archive with the specified options
        /// </summary>
        Task<OperationResult<GameVersion>> InstallGameVersionFromArchiveAsync(
            ExtractOptions options,
            IProgress<InstallProgress>? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Installs a game version from a GitHub Release asset
        /// </summary>
        Task<OperationResult<GameVersion>> InstallVersionFromReleaseAssetAsync(
            GitHubReleaseAsset asset,
            GitHubRelease release,
            string downloadedFilePath,
            ExtractOptions options,
            IProgress<InstallProgress>? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Installs a game version - facade compatibility method
        /// </summary>
        Task<OperationResult> InstallVersionAsync(
            GameVersion version,
            CancellationToken cancellationToken = default,
            IProgress<string>? progress = null);

        /// <summary>
        /// Uninstalls a game version by ID
        /// </summary>
        Task<OperationResult> UninstallVersionAsync(string versionId, CancellationToken cancellationToken = default);
    }
}
