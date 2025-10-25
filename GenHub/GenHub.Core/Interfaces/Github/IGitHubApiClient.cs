using System.Collections.Generic;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Models.GitHub;

namespace GenHub.Core.Interfaces.GitHub;

/// <summary>
/// Interface for GitHub API client operations.
/// </summary>
public interface IGitHubApiClient
{
    /// <summary>
    /// Gets a value indicating whether the client is authenticated.
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// Gets the latest release from the specified repository.
    /// </summary>
    /// <param name="owner">The repository owner.</param>
    /// <param name="repositoryName">The repository name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The latest GitHub release.</returns>
    Task<GitHubRelease> GetLatestReleaseAsync(
        string owner,
        string repositoryName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific release by tag from the specified repository.
    /// </summary>
    /// <param name="owner">The repository owner.</param>
    /// <param name="repositoryName">The repository name.</param>
    /// <param name="tag">The release tag.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The GitHub release with the specified tag.</returns>
    Task<GitHubRelease> GetReleaseByTagAsync(
        string owner,
        string repositoryName,
        string tag,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all releases for the specified repository.
    /// </summary>
    /// <param name="owner">The repository owner.</param>
    /// <param name="repo">The repository name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of GitHub releases.</returns>
    Task<IEnumerable<GitHubRelease>> GetReleasesAsync(
        string owner,
        string repo,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets workflow runs for the specified repository.
    /// </summary>
    /// <param name="owner">The repository owner.</param>
    /// <param name="repo">The repository name.</param>
    /// <param name="perPage">Number of runs per page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of GitHub workflow runs.</returns>
    Task<IEnumerable<GitHubWorkflowRun>> GetWorkflowRunsForRepositoryAsync(
        string owner,
        string repo,
        int perPage = 5,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets artifacts for a workflow run.
    /// </summary>
    /// <param name="owner">The repository owner.</param>
    /// <param name="repo">The repository name.</param>
    /// <param name="runId">The workflow run ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of GitHub artifacts.</returns>
    Task<IEnumerable<GitHubArtifact>> GetArtifactsForWorkflowRunAsync(
        string owner,
        string repo,
        long runId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a release asset to the specified path.
    /// </summary>
    /// <param name="owner">The repository owner.</param>
    /// <param name="repo">The repository name.</param>
    /// <param name="asset">The release asset to download.</param>
    /// <param name="destinationPath">The destination file path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the download operation.</returns>
    Task DownloadReleaseAssetAsync(
        string owner,
        string repo,
        GitHubReleaseAsset asset,
        string destinationPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads an artifact to the specified path.
    /// </summary>
    /// <param name="owner">The repository owner.</param>
    /// <param name="repo">The repository name.</param>
    /// <param name="artifact">The artifact to download.</param>
    /// <param name="destinationPath">The destination file path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the download operation.</returns>
    Task DownloadArtifactAsync(
        string owner,
        string repo,
        GitHubArtifact artifact,
        string destinationPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the authentication token for GitHub API requests.
    /// </summary>
    /// <param name="token">The GitHub token.</param>
    void SetAuthenticationToken(SecureString token);
}
