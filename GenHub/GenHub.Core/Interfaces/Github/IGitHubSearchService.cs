using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Models;

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
            GitHubRepoSettings repository,
            int pullRequestNumber,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Searches workflow runs by text in commit messages, titles, etc.
        /// </summary>
        Task<IEnumerable<GitHubWorkflow>> SearchWorkflowsByTextAsync(
            GitHubRepoSettings repository,
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
    }
}
