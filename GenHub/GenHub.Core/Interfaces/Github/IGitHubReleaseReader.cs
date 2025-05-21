using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Models.GitHub;
using GenHub.Core.Models.AppUpdate;
using GenHub.Core.Models;

namespace GenHub.Core.Interfaces.GitHub
{
    /// <summary>
    /// Interface for GitHub release operations
    /// </summary>
    public interface IGitHubReleaseReader
    {
        /// <summary>
        /// Gets all releases for a repository
        /// </summary>
        Task<IEnumerable<GitHubRelease>> GetReleasesAsync(
            string owner, 
            string repo, 
            int page = 1,
            int perPage = 30,
            bool includePrereleases = true,
            CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets a specific release by tag name
        /// </summary>
        Task<GitHubRelease?> GetReleaseByTagAsync(string owner, string repo, string tag, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets a specific release asset by ID
        /// </summary>
        Task<GitHubReleaseAsset?> GetReleaseAssetAsync(string owner, string repo, long assetId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the latest release for a repository
        /// </summary>
        Task<GitHubRelease?> GetLatestReleaseAsync(string owner, string repo, CancellationToken cancellationToken = default);
    }
}
