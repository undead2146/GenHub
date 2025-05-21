using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Models;
using GenHub.Core.Models.GitHub;


namespace GenHub.Core.Interfaces.GitHub
{
    /// <summary>
    /// Provider for GitHub data used in UI display
    /// </summary>
    public interface IGitHubViewDataProvider
    {
        /// <summary>
        /// Gets workflow files from workflows
        /// </summary>
        Task<IEnumerable<GitHubWorkflow>> GetWorkflowFilesAsync(
            GitHubRepoSettings repository,
            CancellationToken cancellationToken = default);
            
        /// <summary>
        /// Gets workflows from a repository
        /// </summary>
        Task<IEnumerable<GitHubWorkflow>> GetWorkflowsAsync(
            GitHubRepoSettings repository,
            string? workflowPath = null,
            int page = 1,
            int perPage = 30,
            CancellationToken cancellationToken = default);
            
        /// <summary>
        /// Gets releases for display
        /// </summary>
        Task<IEnumerable<GitHubRelease>> GetReleasesForDisplayAsync(
            GitHubRepoSettings repository,
            int page = 1,
            int perPage = 30,
            bool includePrereleases = true,
            CancellationToken cancellationToken = default);
            
        /// <summary>
        /// Searches workflows in a repository
        /// </summary>
        Task<IEnumerable<GitHubWorkflow>> SearchWorkflowsAsync(
            string searchText,
            string searchCriteria,
            CancellationToken cancellationToken = default);
    }
}
