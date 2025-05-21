using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Models;
using GenHub.Core.Models.GitHub;
using GenHub.Core.Models.GameProfiles;
using GenHub.Core.Models.Results;
using GenHub.Core.Interfaces.GitHub;


namespace GenHub.Core.Interfaces
{
    /// <summary>
    /// Facade interface for GitHub API operations, providing a simplified interface
    /// to specialized GitHub services without inheriting all their members.
    /// </summary>
    public interface IGitHubServiceFacade : IGitHubApiClient, IGitHubWorkflowReader
    {
        /// <summary>
        /// Run diagnostic check on GitHub API access
        /// </summary>
        Task<bool> RunDiagnosticCheckAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Get available workflow artifacts for the configured repository
        /// </summary>
        Task<IEnumerable<GitHubArtifact>> GetAvailableArtifactsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Download a specific artifact by ID
        /// </summary>
        Task<string> DownloadArtifactAsync(
            long artifactId,
            string destinationFolder,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default);
        // Add this method to the interface
        /// <summary>
        /// Checks if the GitHub API is accessible
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>True if the API is accessible, otherwise false</returns>
        Task<bool> IsApiAccessibleAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Get workflow runs from the default repository
        /// </summary>
        Task<IEnumerable<GitHubWorkflow>> GetWorkflowRunsAsync(
            int page = 1,
            int perPage = 10,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get workflow runs from a specific repository
        /// </summary>
        Task<IEnumerable<GitHubWorkflow>> GetWorkflowRunsForRepositoryAsync(
            GitHubRepoSettings repoConfig,
            int page = 1,
            int perPage = 10,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get a specific workflow run by run number
        /// </summary>
        Task<GitHubWorkflow?> GetWorkflowRunByNumberAsync(
            GitHubRepoSettings repoConfig,
            int runNumber,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get artifacts for a specific workflow run
        /// </summary>
        Task<IEnumerable<GitHubArtifact>> GetArtifactsForRunAsync(
            GitHubWorkflow run,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets artifacts for a specific workflow run by run ID
        /// </summary>
        Task<IEnumerable<GitHubArtifact>> GetArtifactsForWorkflowAsync(
            long runId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get artifacts from a specific repository
        /// </summary>
        Task<IEnumerable<GitHubArtifact>> GetArtifactsForRepositoryAsync(
            GitHubRepoSettings repoConfig,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Download an artifact from a specific repository
        /// </summary>
        Task<string> DownloadArtifactFromRepositoryAsync(
            GitHubRepoSettings repoConfig,
            long artifactId,
            string destinationFolder,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get all configured repositories
        /// </summary>
        IEnumerable<GitHubRepoSettings> GetRepositories();

        /// <summary>
        /// Get default repository
        /// </summary>
        GitHubRepoSettings GetDefaultRepository();

        /// <summary>
        /// Set authentication token for GitHub API
        /// </summary>
        void SetAuthToken(string token);

        /// <summary>
        /// Get current authentication token for GitHub API
        /// </summary>
        string? GetAuthToken();

        /// <summary>
        /// Checks if an authentication token exists
        /// </summary>
        bool HasAuthToken();

        /// <summary>
        /// Searches for workflow runs related to a specific pull request number
        /// </summary>
        Task<List<GitHubWorkflow>> SearchWorkflowsByPullRequestAsync(
            int pullRequestNumber,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Searches for workflow runs related to a specific pull request number in a specific repository
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

        /// <summary>
        /// Gets the URL to view a workflow run on GitHub
        /// </summary>
        string GetWorkflowRunUrl(GitHubArtifact artifact);

        /// <summary>
        /// Gets the URL to view a workflow run on GitHub by run ID
        /// </summary>
        string GetWorkflowRunUrl(long runId);

        /// <summary>
        /// Gets workflow runs for a specific workflow file
        /// </summary>
        Task<IEnumerable<GitHubWorkflow>> GetWorkflowRunsForWorkflowFileAsync(
            GitHubRepoSettings repoConfig,
            string workflowFile,
            int page = 1,
            int perPage = 10,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Parses build information from an artifact name
        /// </summary>
        GitHubBuild ParseBuildInfo(string artifactName);

        /// <summary>
        /// Gets the list of detected versions from GitHub artifacts
        /// </summary>
        Task<IEnumerable<GameVersion>> GetDetectedVersionsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the list of detected versions from GitHub artifacts for a specific repository
        /// </summary>
        Task<IEnumerable<GameVersion>> GetDetectedVersionsAsync(
            GitHubRepoSettings repoConfig,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets releases for a repository
        /// </summary>
        Task<IEnumerable<GitHubRelease>> GetReleasesAsync(
            GitHubRepoSettings repoSettings,
            int page = 1,
            int perPage = 30,
            bool includePrereleases = true,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Downloads a release asset
        /// </summary>
        Task<(Stream Stream, long? ContentLength)> DownloadReleaseAssetAsync(
            string assetDownloadUrl,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Installs a GitHub artifact
        /// </summary>
        Task<GameVersion?> InstallArtifactAsync(
            GitHubArtifact artifact,
            IProgress<InstallProgress>? progress = null,
            CancellationToken cancellationToken = default);
    }
}
