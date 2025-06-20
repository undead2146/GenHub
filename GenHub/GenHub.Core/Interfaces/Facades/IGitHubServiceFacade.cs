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
using GenHub.Core.Models.Enums;


namespace GenHub.Core.Interfaces
{
    /// <summary>
    /// Facade interface for GitHub API operations, providing a simplified interface
    /// to specialized GitHub services without inheriting all their members.
    /// </summary>
    public interface IGitHubServiceFacade : IGitHubApiClient, IGitHubWorkflowReader
    {
        /// <summary>
        /// Repository discovery service
        /// </summary>
        IGitHubRepositoryDiscoveryService RepositoryDiscovery { get; }
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
            GitHubRepository repoConfig,
            int page = 1,
            int perPage = 10,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get a specific workflow run by run number
        /// </summary>
        Task<GitHubWorkflow?> GetWorkflowRunByNumberAsync(
            GitHubRepository repoConfig,
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
            GitHubRepository repoConfig,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Download an artifact from a specific repository
        /// </summary>
        Task<string> DownloadArtifactFromRepositoryAsync(
            GitHubRepository repoConfig,
            long artifactId,
            string destinationFolder,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get all configured repositories
        /// </summary>
        IEnumerable<GitHubRepository> GetRepositories();

        /// <summary>
        /// Get default repository
        /// </summary>
        GitHubRepository GetDefaultRepository();

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
            GitHubRepository repoConfig,
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
            GitHubRepository repoConfig,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets releases for a repository
        /// </summary>
        Task<IEnumerable<GitHubRelease>> GetReleasesAsync(
            GitHubRepository repoSettings,
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
