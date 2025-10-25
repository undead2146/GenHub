using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Models.GitHub;
using GenHub.Core.Models.Results;

/// <summary>
/// Defines the facade interface for GitHub service operations in GenHub.
/// Provides high-level methods for repository, release, workflow, and download operations.
/// </summary>
public interface IGitHubServiceFacade
{
    /// <summary>
    /// Gets the latest release for a specified repository asynchronously.
    /// </summary>
    /// <param name="owner">The repository owner (username or organization).</param>
    /// <param name="repositoryName">The name of the repository.</param>
    /// <returns>The latest <see cref="GitHubRelease"/> for the repository.</returns>
    /// <exception cref="InvalidOperationException">Thrown if no releases are found.</exception>
    Task<GitHubRelease> GetLatestReleaseAsync(string owner, string repositoryName);

    /// <summary>
    /// Gets a specific release by tag name for a repository asynchronously.
    /// </summary>
    /// <param name="owner">The repository owner (username or organization).</param>
    /// <param name="repositoryName">The name of the repository.</param>
    /// <param name="tag">The tag name of the release to retrieve.</param>
    /// <returns>The <see cref="GitHubRelease"/> matching the specified tag.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the release is not found.</exception>
    Task<GitHubRelease> GetReleaseByTagAsync(string owner, string repositoryName, string tag);

    /// <summary>
    /// Gets all releases for a specified repository asynchronously.
    /// </summary>
    /// <param name="owner">The repository owner (username or organization).</param>
    /// <param name="repo">The name of the repository.</param>
    /// <returns>An <see cref="OperationResult{T}"/> containing the list of <see cref="GitHubRelease"/> objects.</returns>
    Task<OperationResult<IEnumerable<GitHubRelease>>> GetReleasesAsync(string owner, string repo);

    /// <summary>
    /// Gets workflow runs for a specified repository asynchronously.
    /// </summary>
    /// <param name="owner">The repository owner (username or organization).</param>
    /// <param name="repo">The name of the repository.</param>
    /// <param name="perPage">The number of workflow runs to retrieve per page (default: 5).</param>
    /// <returns>An <see cref="OperationResult{T}"/> containing the list of <see cref="GitHubWorkflowRun"/> objects.</returns>
    Task<OperationResult<IEnumerable<GitHubWorkflowRun>>> GetWorkflowRunsForRepositoryAsync(string owner, string repo, int perPage = 5);

    /// <summary>
    /// Gets artifacts for a specific workflow run asynchronously.
    /// </summary>
    /// <param name="owner">The repository owner (username or organization).</param>
    /// <param name="repo">The name of the repository.</param>
    /// <param name="runId">The ID of the workflow run.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>An <see cref="OperationResult{T}"/> containing the list of <see cref="GitHubArtifact"/> objects.</returns>
    Task<OperationResult<IEnumerable<GitHubArtifact>>> GetArtifactsForWorkflowRunAsync(string owner, string repo, long runId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a release asset asynchronously.
    /// </summary>
    /// <param name="owner">The repository owner (username or organization).</param>
    /// <param name="repo">The name of the repository.</param>
    /// <param name="asset">The <see cref="GitHubReleaseAsset"/> to download.</param>
    /// <param name="destinationPath">The local path to save the asset.</param>
    /// <returns>A <see cref="DownloadResult"/> indicating the download outcome.</returns>
    Task<DownloadResult> DownloadReleaseAssetAsync(string owner, string repo, GitHubReleaseAsset asset, string destinationPath);

    /// <summary>
    /// Downloads an artifact asynchronously.
    /// </summary>
    /// <param name="owner">The repository owner (username or organization).</param>
    /// <param name="repo">The name of the repository.</param>
    /// <param name="artifact">The <see cref="GitHubArtifact"/> to download.</param>
    /// <param name="destinationPath">The local path to save the artifact.</param>
    /// <returns>A <see cref="DownloadResult"/> indicating the download outcome.</returns>
    Task<DownloadResult> DownloadArtifactAsync(string owner, string repo, GitHubArtifact artifact, string destinationPath);
}
