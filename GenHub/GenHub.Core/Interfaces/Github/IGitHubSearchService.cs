using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Models;
using GenHub.Core.Models.Enums;

namespace GenHub.Core.Interfaces
{
    /// <summary>
    /// Service for GitHub search operations
    /// </summary>
    public interface IGitHubSearchService
    {
        /// <summary>
        /// Searches for workflow runs related to a specific pull request number
        /// </summary>
        Task<List<GitHubWorkflow>> SearchWorkflowsByPullRequestAsync(
            int pullRequestNumber,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Searches workflows based on text and criteria
        /// </summary>
        Task<IEnumerable<GitHubWorkflow>> SearchWorkflowsAsync(
            string searchText,
            string searchCriteria,
            CancellationToken cancellationToken = default);
            
        /// <summary>
        /// Searches for workflow runs related to a specific pull request number
        /// </summary>
        Task<IEnumerable<GitHubWorkflow>> SearchWorkflowsByPullRequestAsync(
            GitHubRepository repository,
            int pullRequestNumber,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Searches workflow runs by text in commit messages, titles, etc.
        /// </summary>
        Task<IEnumerable<GitHubWorkflow>> SearchWorkflowsByTextAsync(
            GitHubRepository repository,
            string searchText,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Searches for workflow runs by workflow number
        /// </summary>
        Task<List<GitHubWorkflow>> SearchWorkflowsByWorkflowNumberAsync(
            int workflowNumber,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Searches workflow runs by text in commit messages
        /// </summary>
        Task<List<GitHubWorkflow>> SearchWorkflowsByCommitMessageAsync(
            string searchText,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Context-aware search method that supports both "All Items" and specific workflow contexts
        /// </summary>
        /// <param name="repository">Repository context for the search</param>
        /// <param name="searchText">Search query text</param>
        /// <param name="searchCriteria">Type of search to perform</param>
        /// <param name="workflowPath">Optional workflow file path (null = all items)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Collection of matching workflows</returns>
        Task<IEnumerable<GitHubWorkflow>> SearchWithContextAsync(
            GitHubRepository repository,
            string searchText,
            GitHubSearchCriteria searchCriteria,
            string? workflowPath = null,
            CancellationToken cancellationToken = default);
    }
}
