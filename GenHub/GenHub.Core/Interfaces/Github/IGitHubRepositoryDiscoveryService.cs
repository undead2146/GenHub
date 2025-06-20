using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Models;
using GenHub.Core.Models.GitHub;
using GenHub.Core.Models.Results;

namespace GenHub.Core.Interfaces.GitHub
{
    /// <summary>
    /// Service for discovering Command & Conquer related GitHub repositories through intelligent fork traversal
    /// </summary>
    public interface IGitHubRepositoryDiscoveryService
    {
        /// <summary>
        /// Discovers C&C repositories by intelligently traversing fork networks from base repositories
        /// </summary>
        /// <param name="options">Discovery configuration options</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Operation result containing discovered repositories</returns>
        Task<OperationResult<IEnumerable<GitHubRepository>>> DiscoverRepositoriesAsync(
            DiscoveryOptions? options = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Discovers forks of specific repositories with intelligent activity-based filtering
        /// </summary>
        /// <param name="owner">Repository owner</param>
        /// <param name="name">Repository name</param>
        /// <param name="options">Discovery options</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Active forks of the specified repository</returns>
        Task<OperationResult<IEnumerable<GitHubRepository>>> DiscoverActiveForks(
            string owner,
            string name,
            DiscoveryOptions? options = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Finds the most active and relevant forks in a repository network
        /// </summary>
        /// <param name="repositories">Base repositories to analyze</param>
        /// <param name="options">Discovery options</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Ranked list of active repositories</returns>
        Task<OperationResult<IEnumerable<GitHubRepository>>> FindMostActiveForks(
            IEnumerable<GitHubRepository> repositories,
            DiscoveryOptions? options = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Adds discovered repositories to the managed repository list
        /// </summary>
        /// <param name="repositories">Repositories to add</param>
        /// <param name="replaceExisting">Whether to update existing repositories</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Operation result</returns>
        Task<OperationResult> AddDiscoveredRepositoriesAsync(
            IEnumerable<GitHubRepository> repositories,
            bool replaceExisting = false,
            CancellationToken cancellationToken = default);
    }
}
