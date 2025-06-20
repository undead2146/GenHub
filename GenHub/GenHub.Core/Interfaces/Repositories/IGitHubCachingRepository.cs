using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Models;

namespace GenHub.Core.Interfaces.Repositories
{
    /// <summary>
    /// Repository for managing GitHub data (workflows, artifacts, etc.)
    /// </summary>
    public interface IGitHubCachingRepository
    {
        /// <summary>
        /// Caches workflow data for a repository
        /// </summary>
        Task CacheWorkflowsAsync(string repoFullName, IEnumerable<GitHubWorkflow> workflows, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets cached workflows for a repository
        /// </summary>
        Task<IEnumerable<GitHubWorkflow>> GetCachedWorkflowsAsync(string repoFullName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Saves a collection of workflows to the cache for a specific repository
        /// </summary>
        /// <param name="workflows">The list of GitHubWorkflow objects to save</param>
        /// <param name="repositoryId">The unique identifier of the repository (e.g., "owner/repoName").</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task SaveWorkflowsAsync(IEnumerable<GitHubWorkflow> workflows, string repositoryId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Caches artifacts for a workflow run
        /// </summary>
        Task CacheArtifactsAsync(string repoFullName, long runId, IEnumerable<GitHubArtifact> artifacts, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets cached artifacts for a workflow run
        /// </summary>
        Task<IEnumerable<GitHubArtifact>> GetCachedArtifactsAsync(string repoFullName, long runId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Determines if a cache is expired
        /// </summary>
        bool IsCacheExpired(string key, TimeSpan duration);

        /// <summary>
        /// Gets all configured repositories
        /// </summary>
        Task<IEnumerable<GitHubRepository>> GetRepositoriesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Saves the list of repositories
        /// </summary>
        Task SaveRepositoriesAsync(IEnumerable<GitHubRepository> repositories, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the currently selected repository
        /// </summary>
        Task<GitHubRepository?> GetCurrentRepositoryAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Saves the currently selected repository
        /// </summary>
        Task SaveCurrentRepositoryAsync(GitHubRepository repository, CancellationToken cancellationToken = default);
    }
}
