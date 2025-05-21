using System;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Models;
using GenHub.Core.Models.GameProfiles;
using GenHub.Core.Models.Results;
using GenHub.Core.Models.GitHub;

namespace GenHub.Core.Interfaces
{
    /// <summary>
    /// Service for installing game versions
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
        /// Installs a game version from an archive (ZIP) with the specified options
        /// </summary>
        Task<OperationResult<GameVersion>> InstallGameVersionFromArchiveAsync(
            ExtractOptions options,
            IProgress<InstallProgress>? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Installs a game version from a GitHub release asset
        /// </summary>
        Task<OperationResult<GameVersion>> InstallVersionFromReleaseAssetAsync(
            GitHubReleaseAsset asset,
            GitHubRelease release, 
            string downloadedFilePath,
            ExtractOptions options,
            IProgress<InstallProgress>? progress = null,
            CancellationToken cancellationToken = default);
            
        /// <summary>
        /// Uninstalls a game version
        /// </summary>
        Task<OperationResult> UninstallVersionAsync(string versionId);
    }
}
