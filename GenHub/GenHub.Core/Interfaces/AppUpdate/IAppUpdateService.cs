// filepath: z:\GenHub\GenHub\GenHub.Core\Interfaces\AppUpdate\IAppUpdateService.cs
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Models.AppUpdate;
using GenHub.Core.Models;
using GenHub.Core.Models.GitHub;

namespace GenHub.Core.Interfaces.AppUpdate
{
    /// <summary>
    /// Interface for application update services.
    /// </summary>
    public interface IAppUpdateService
    {
        /// <summary>
        /// Checks if updates are available for the application.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>Result of the update check.</returns>
        Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets the latest release information.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>Information about the latest release if available.</returns>
        Task<GitHubRelease?> GetLatestReleaseAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets the latest release for a specific repository.
        /// </summary>
        /// <param name="owner">The repository owner.</param>
        /// <param name="repo">The repository name.</param>
        /// <param name="forceFresh">Whether to bypass cache and fetch fresh data.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>Information about the latest release if available.</returns>
        Task<GitHubRelease?> GetLatestReleaseForRepoAsync(string owner, string repo, bool forceFresh = false, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Checks for updates from a specific repository.
        /// </summary>
        /// <param name="owner">The repository owner.</param>
        /// <param name="repo">The repository name.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>Result of the update check.</returns>
        Task<UpdateCheckResult> CheckForUpdatesAsync(string owner, string repo, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Checks for updates bypassing cache.
        /// </summary>
        /// <param name="owner">The repository owner.</param>
        /// <param name="repo">The repository name.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>Result of the update check.</returns>
        Task<UpdateCheckResult> CheckForUpdatesNoCache(string owner, string repo, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets the current application version.
        /// </summary>
        /// <returns>The current version of the application.</returns>
        string GetCurrentVersion();
        
        /// <summary>
        /// Gets the repository settings used for update checking.
        /// </summary>
        /// <returns>Repository settings.</returns>
        Task<GitHubRepository> GetRepositorySettingsAsync();
        
        /// <summary>
        /// Saves the repository settings used for update checking.
        /// </summary>
        /// <param name="settings">Repository settings to save.</param>
        Task SaveRepositorySettingsAsync(GitHubRepository settings);
        
        /// <summary>
        /// Updates the application to the specified release.
        /// </summary>
        /// <param name="release">Release to update to.</param>
        /// <param name="progressReporter">Progress reporter.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        Task UpdateApplicationAsync(GitHubRelease release, IProgress<UpdateProgress>? progressReporter = null, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Initiates the update installation process.
        /// </summary>
        /// <param name="release">Release to install.</param>
        /// <param name="progressReporter">Progress reporter.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        Task InitiateUpdateAsync(GitHubRelease release, IProgress<UpdateProgress>? progressReporter = null, CancellationToken cancellationToken = default);
    }
}
