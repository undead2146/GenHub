using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Models;

namespace GenHub.Core.Interfaces.GitHub
{
    /// <summary>
    /// Service for reading GitHub workflow runs
    /// </summary>
    public interface IGitHubWorkflowReader
    {
        /// <summary>
        /// Gets workflow runs for a repository
        /// </summary>
        Task<IEnumerable<GitHubWorkflow>> GetWorkflowRunsForRepositoryAsync(
            GitHubRepository repoSettings,
            int page = 1,
            int perPage = 30,
            CancellationToken cancellationToken = default);
            
        /// <summary>
        /// Gets a specific workflow run by its ID
        /// </summary>
        Task<GitHubWorkflow?> GetWorkflowRunAsync(
            GitHubRepository repoSettings, 
            long runId,
            CancellationToken cancellationToken = default);
            
        /// <summary>
        /// Gets artifacts for workflow runs
        /// </summary>
        Task<IDictionary<long, int>> GetArtifactCountsForWorkflowsAsync(
            IEnumerable<long> workflowIds,
            CancellationToken cancellationToken = default);
    }
}
