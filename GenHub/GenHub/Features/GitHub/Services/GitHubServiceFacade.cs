using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Interfaces.GitHub;
using GenHub.Core.Models.GitHub;
using GenHub.Core.Models.Results;
using Microsoft.Extensions.Logging;

namespace GenHub.Features.GitHub.Services;

/// <summary>
/// Facade service for GitHub operations, providing high-level API for GitHub manager functionality.
/// </summary>
public class GitHubServiceFacade(
    IGitHubApiClient gitHubApiClient,
    ILogger<GitHubServiceFacade> logger) : IGitHubServiceFacade
{
    /// <summary>
    /// Gets repositories for the authenticated user.
    /// </summary>
    /// <returns>A result containing the list of repositories.</returns>
    public async Task<OperationResult<List<GitHubRepository>>> GetUserRepositoriesAsync()
    {
        try
        {
            logger.LogDebug("Getting user repositories");

            var repositories = new List<GitHubRepository>();

            logger.LogInformation("Retrieved {Count} user repositories", repositories.Count);
            return await Task.FromResult(OperationResult<List<GitHubRepository>>.CreateSuccess(repositories));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get user repositories");
            return await Task.FromResult(OperationResult<List<GitHubRepository>>.CreateFailure($"Failed to get user repositories: {ex.Message}"));
        }
    }

    /// <summary>
    /// Gets the latest release for a repository.
    /// </summary>
    /// <param name="owner">The repository owner.</param>
    /// <param name="repositoryName">The repository name.</param>
    /// <returns>The latest release.</returns>
    public async Task<GitHubRelease> GetLatestReleaseAsync(string owner, string repositoryName)
    {
        var releasesResult = await GetReleasesAsync(owner, repositoryName);
        if (releasesResult.Success && releasesResult.Data?.Any() == true)
        {
            var data = releasesResult.Data;
            return data.OrderByDescending(r => r.PublishedAt).First();
        }

        throw new InvalidOperationException("No releases found for the repository.");
    }

    /// <summary>
    /// Gets a specific release by tag.
    /// </summary>
    /// <param name="owner">The repository owner.</param>
    /// <param name="repositoryName">The repository name.</param>
    /// <param name="tag">The release tag.</param>
    /// <returns>The release matching the tag.</returns>
    public async Task<GitHubRelease> GetReleaseByTagAsync(string owner, string repositoryName, string tag)
    {
        var releasesResult = await GetReleasesAsync(owner, repositoryName);
        if (releasesResult.Success)
        {
            var data = releasesResult.Data;
            var release = data?.FirstOrDefault(r => r.TagName == tag);
            if (release != null)
            {
                return release;
            }
        }

        throw new InvalidOperationException($"Release with tag '{tag}' not found.");
    }

    /// <summary>
    /// Gets releases for a specific repository.
    /// </summary>
    /// <param name="owner">The repository owner.</param>
    /// <param name="repo">The repository name.</param>
    /// <returns>A result containing the releases.</returns>
    public async Task<OperationResult<IEnumerable<GitHubRelease>>> GetReleasesAsync(string owner, string repo)
    {
        var result = await GetRepositoryReleasesAsync(owner, repo);

        if (result.Success)
        {
            return OperationResult<IEnumerable<GitHubRelease>>.CreateSuccess(result.Data!);
        }

        return OperationResult<IEnumerable<GitHubRelease>>.CreateFailure(result.Errors);
    }

    /// <summary>
    /// Gets releases for a specific repository.
    /// </summary>
    /// <param name="owner">The repository owner.</param>
    /// <param name="repositoryName">The repository name.</param>
    /// <returns>A result containing the list of releases.</returns>
    public async Task<OperationResult<List<GitHubRelease>>> GetRepositoryReleasesAsync(
        string owner,
        string repositoryName)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            logger.LogDebug("Getting releases for repository {Owner}/{Repository}", owner, repositoryName);

            var releases = await gitHubApiClient.GetReleasesAsync(owner, repositoryName);
            var releaseList = releases.ToList();

            logger.LogInformation("Retrieved {Count} releases for {Owner}/{Repository}", releaseList.Count, owner, repositoryName);
            return OperationResult<List<GitHubRelease>>.CreateSuccess(releaseList, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.LogError(ex, "Failed to get releases for repository {Owner}/{Repository}", owner, repositoryName);
            return OperationResult<List<GitHubRelease>>.CreateFailure("Failed to get releases", stopwatch.Elapsed);
        }
    }

    /// <summary>
    /// Gets workflows for a specific repository.
    /// </summary>
    /// <param name="owner">The repository owner.</param>
    /// <param name="repositoryName">The repository name.</param>
    /// <returns>A result containing the list of workflows.</returns>
    public async Task<OperationResult<List<GitHubWorkflow>>> GetRepositoryWorkflowsAsync(
        string owner,
        string repositoryName)
    {
        try
        {
            logger.LogDebug("Getting workflows for repository {Owner}/{Repository}", owner, repositoryName);

            var workflows = new List<GitHubWorkflow>();

            logger.LogInformation("Retrieved {Count} workflows for {Owner}/{Repository}", workflows.Count, owner, repositoryName);
            return await Task.FromResult(OperationResult<List<GitHubWorkflow>>.CreateSuccess(workflows));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get workflows for repository {Owner}/{Repository}", owner, repositoryName);
            return await Task.FromResult(OperationResult<List<GitHubWorkflow>>.CreateFailure($"Failed to get workflows: {ex.Message}"));
        }
    }

    /// <summary>
    /// Gets workflow runs for a specific repository.
    /// </summary>
    /// <param name="owner">The repository owner.</param>
    /// <param name="repo">The repository name.</param>
    /// <param name="perPage">Number of runs per page.</param>
    /// <returns>A result containing the list of workflow runs.</returns>
    public async Task<OperationResult<IEnumerable<GitHubWorkflowRun>>> GetWorkflowRunsForRepositoryAsync(string owner, string repo, int perPage = 5)
    {
        var result = await GetRepositoryWorkflowRunsAsync(owner, repo, perPage);

        if (result.Success)
        {
            return OperationResult<IEnumerable<GitHubWorkflowRun>>.CreateSuccess(result.Data!);
        }

        return OperationResult<IEnumerable<GitHubWorkflowRun>>.CreateFailure(result.Errors);
    }

    /// <summary>
    /// Gets workflow runs for a specific repository.
    /// </summary>
    /// <param name="owner">The repository owner.</param>
    /// <param name="repositoryName">The repository name.</param>
    /// <param name="perPage">Number of runs to retrieve.</param>
    /// <returns>A result containing the list of workflow runs.</returns>
    public async Task<OperationResult<List<GitHubWorkflowRun>>> GetRepositoryWorkflowRunsAsync(
        string owner,
        string repositoryName,
        int perPage = 10)
    {
        try
        {
            logger.LogDebug("Getting workflow runs for repository {Owner}/{Repository}", owner, repositoryName);

            var workflowRuns = await gitHubApiClient.GetWorkflowRunsForRepositoryAsync(owner, repositoryName, perPage);
            var workflowRunList = workflowRuns.ToList();

            logger.LogInformation("Retrieved {Count} workflow runs for {Owner}/{Repository}", workflowRunList.Count, owner, repositoryName);
            return OperationResult<List<GitHubWorkflowRun>>.CreateSuccess(workflowRunList);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get workflow runs for repository {Owner}/{Repository}", owner, repositoryName);
            return OperationResult<List<GitHubWorkflowRun>>.CreateFailure($"Failed to get workflow runs: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets artifacts for a specific workflow run.
    /// </summary>
    /// <param name="owner">The repository owner.</param>
    /// <param name="repo">The repository name.</param>
    /// <param name="runId">The workflow run ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A result containing the list of artifacts.</returns>
    public async Task<OperationResult<IEnumerable<GitHubArtifact>>> GetArtifactsForWorkflowRunAsync(string owner, string repo, long runId, CancellationToken cancellationToken = default)
    {
        var result = await GetWorkflowRunArtifactsAsync(owner, repo, runId, cancellationToken);

        if (result.Success)
        {
            return OperationResult<IEnumerable<GitHubArtifact>>.CreateSuccess(result.Data!);
        }

        return OperationResult<IEnumerable<GitHubArtifact>>.CreateFailure(result.Errors);
    }

    /// <summary>
    /// Gets artifacts for a specific workflow run.
    /// </summary>
    /// <param name="owner">The repository owner.</param>
    /// <param name="repositoryName">The repository name.</param>
    /// <param name="runId">The workflow run ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A result containing the list of artifacts.</returns>
    public async Task<OperationResult<List<GitHubArtifact>>> GetWorkflowRunArtifactsAsync(
        string owner,
        string repositoryName,
        long runId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogDebug("Getting artifacts for workflow run {RunId} in {Owner}/{Repository}", runId, owner, repositoryName);

            var artifacts = await gitHubApiClient.GetArtifactsForWorkflowRunAsync(owner, repositoryName, runId, cancellationToken);
            var artifactList = artifacts.ToList();

            logger.LogInformation("Retrieved {Count} artifacts for workflow run {RunId}", artifactList.Count, runId);
            return OperationResult<List<GitHubArtifact>>.CreateSuccess(artifactList);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get artifacts for workflow run {RunId}", runId);
            return OperationResult<List<GitHubArtifact>>.CreateFailure($"Failed to get artifacts: {ex.Message}");
        }
    }

    /// <summary>
    /// Downloads an artifact from a workflow run.
    /// </summary>
    /// <param name="owner">The repository owner.</param>
    /// <param name="repo">The repository name.</param>
    /// <param name="artifact">The artifact to download.</param>
    /// <param name="destinationPath">The destination path for the download.</param>
    /// <returns>A download result.</returns>
    public async Task<DownloadResult> DownloadArtifactAsync(
        string owner,
        string repo,
        GitHubArtifact artifact,
        string destinationPath)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            logger.LogDebug("Downloading artifact {ArtifactId} to {Destination}", artifact.Id, destinationPath);

            await gitHubApiClient.DownloadArtifactAsync(owner, repo, artifact, destinationPath);

            var bytesDownloaded = artifact.SizeInBytes;
            logger.LogInformation("Successfully downloaded artifact {ArtifactId}", artifact.Id);
            return DownloadResult.CreateSuccess(destinationPath, bytesDownloaded, stopwatch.Elapsed, hashVerified: false);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.LogError(ex, "Failed to download artifact {ArtifactId} from {Owner}/{Repo}", artifact.Id, owner, repo);
            return DownloadResult.CreateFailure($"Download failed: {ex.Message}", 0, stopwatch.Elapsed);
        }
    }

    /// <summary>
    /// Downloads a release asset.
    /// </summary>
    /// <param name="owner">The repository owner.</param>
    /// <param name="repo">The repository name.</param>
    /// <param name="asset">The release asset to download.</param>
    /// <param name="destinationPath">The destination path for the download.</param>
    /// <returns>A download result.</returns>
    public async Task<DownloadResult> DownloadReleaseAssetAsync(
        string owner,
        string repo,
        GitHubReleaseAsset asset,
        string destinationPath)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            logger.LogDebug("Downloading release asset {AssetId} to {Destination}", asset.Id, destinationPath);

            await gitHubApiClient.DownloadReleaseAssetAsync(owner, repo, asset, destinationPath);

            var bytesDownloaded = asset.Size;
            logger.LogInformation("Successfully downloaded release asset {AssetId}", asset.Id);
            return DownloadResult.CreateSuccess(destinationPath, bytesDownloaded, stopwatch.Elapsed, hashVerified: false);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.LogError(ex, "Failed to download release asset {AssetId}", asset.Id);
            return DownloadResult.CreateFailure($"Download failed: {ex.Message}", 0, stopwatch.Elapsed);
        }
    }
}
