using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Interfaces.GitHub;
using GenHub.Core.Models.GitHub;
using GenHub.Core.Models.Results;
using Microsoft.Extensions.Logging;
using Octokit;

namespace GenHub.Features.GitHub.Services;

/// <summary>
/// GitHub API client implementation using Octokit.
/// </summary>
public class OctokitGitHubApiClient : IGitHubApiClient
{
    private readonly IGitHubClient _gitHubClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OctokitGitHubApiClient> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OctokitGitHubApiClient"/> class.
    /// </summary>
    /// <param name="gitHubClient">The Octokit GitHub client.</param>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="logger">The logger instance.</param>
    public OctokitGitHubApiClient(
        IGitHubClient gitHubClient,
        IHttpClientFactory httpClientFactory,
        ILogger<OctokitGitHubApiClient> logger)
    {
        _gitHubClient = gitHubClient ?? throw new ArgumentNullException(nameof(gitHubClient));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets a value indicating whether the client is authenticated.
    /// </summary>
    public bool IsAuthenticated => ((GitHubClient)_gitHubClient).Credentials != Credentials.Anonymous;

    private SecureString? _token;

    /// <summary>
    /// Sets the GitHub token for authentication.
    /// </summary>
    /// <param name="token">The GitHub token.</param>
    public void SetToken(SecureString token)
    {
        _token = token;
        if (_gitHubClient is GitHubClient client)
        {
            var tokenString = new System.Net.NetworkCredential(string.Empty, _token).Password;
            client.Credentials = new Credentials(tokenString);
        }
    }

    /// <summary>
    /// Gets the current GitHub token.
    /// </summary>
    /// <returns>The GitHub token.</returns>
    public SecureString GetToken() => _token!;

    /// <summary>
    /// Downloads a release asset to the specified path.
    /// </summary>
    /// <param name="owner">The repository owner.</param>
    /// <param name="repo">The repository name.</param>
    /// <param name="asset">The release asset to download.</param>
    /// <param name="destinationPath">The destination file path.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the download operation.</returns>
    public async Task DownloadReleaseAssetAsync(
        string owner,
        string repo,
        GitHubReleaseAsset asset,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Downloading release asset {AssetId} to {Destination}", asset.Id, destinationPath);

            var httpClient = _httpClientFactory.CreateClient("GitHubApi");
            using var response = await httpClient.GetAsync(asset.BrowserDownloadUrl, cancellationToken);
            response.EnsureSuccessStatusCode();
            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var fileStream = File.Create(destinationPath);
            await contentStream.CopyToAsync(fileStream, cancellationToken);

            _logger.LogInformation("Successfully downloaded release asset {AssetId}", asset.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download release asset {AssetId}", asset.Id);
            throw;
        }
    }

    /// <summary>
    /// Downloads an artifact to the specified path.
    /// </summary>
    /// <param name="owner">The repository owner.</param>
    /// <param name="repo">The repository name.</param>
    /// <param name="artifact">The artifact to download.</param>
    /// <param name="destinationPath">The destination file path.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the download operation.</returns>
    public async Task DownloadArtifactAsync(
        string owner,
        string repo,
        GitHubArtifact artifact,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting download of artifact {ArtifactId} from {Owner}/{Repo}", artifact.Id, owner, repo);

            // Check authentication status first
            if (_gitHubClient is GitHubClient concreteClient)
            {
                var isAuth = concreteClient.Credentials != Credentials.Anonymous;
                var authType = concreteClient.Credentials?.AuthenticationType.ToString() ?? "None";
                _logger.LogInformation("Authentication status: IsAuthenticated={IsAuth}, Type={AuthType}", isAuth, authType);

                if (!isAuth)
                {
                    _logger.LogError("No authentication available for artifact download. Please configure a GitHub token.");
                    throw new InvalidOperationException("GitHub authentication required for artifact downloads. Please configure a GitHub token.");
                }
            }

            var artifactUrl = $"https://api.github.com/repos/{owner}/{repo}/actions/artifacts/{artifact.Id}/zip";
            _logger.LogInformation("Requesting artifact from URL: {Url}", artifactUrl);

            var httpClient = _httpClientFactory.CreateClient("GitHubApi");
            httpClient.DefaultRequestHeaders.Add("User-Agent", "GenHub/1.0");

            // Add authentication headers
            if (_gitHubClient is GitHubClient authClient && authClient.Credentials != Credentials.Anonymous)
            {
                if (authClient.Credentials.AuthenticationType == AuthenticationType.Bearer)
                {
                    httpClient.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authClient.Credentials.Password);
                    _logger.LogInformation("Added Bearer authentication header");
                }
                else if (authClient.Credentials.AuthenticationType == AuthenticationType.Basic)
                {
                    var authValue = Convert.ToBase64String(
                        System.Text.Encoding.ASCII.GetBytes($"{authClient.Credentials.Login}:{authClient.Credentials.Password}"));
                    httpClient.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authValue);
                    _logger.LogInformation("Added Basic authentication header for user: {Login}", authClient.Credentials.Login);
                }
            }
            else
            {
                _logger.LogError("Failed to add authentication headers - no valid credentials found");
                throw new InvalidOperationException("Authentication failed - no valid GitHub credentials available");
            }

            var response = await httpClient.GetAsync(artifactUrl, cancellationToken);
            _logger.LogInformation("Received HTTP response: {StatusCode} {ReasonPhrase}", response.StatusCode, response.ReasonPhrase);

            if (!response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("HTTP request failed with status {StatusCode}: {Content}", response.StatusCode, responseContent);
            }

            response.EnsureSuccessStatusCode();

            await using var fileStream = File.Create(destinationPath);
            await response.Content.CopyToAsync(fileStream, cancellationToken);

            _logger.LogInformation("Successfully downloaded artifact {ArtifactId}", artifact.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download artifact {ArtifactId}", artifact.Id);
            throw;
        }
    }

    /// <summary>
    /// Gets the latest release for the specified repository.
    /// </summary>
    /// <param name="owner">The repository owner.</param>
    /// <param name="repositoryName">The repository name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The latest <see cref="GitHubRelease"/> or null if not found.</returns>
    public async Task<GitHubRelease> GetLatestReleaseAsync(
        string owner,
        string repositoryName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var octo = await _gitHubClient.Repository.Release.GetLatest(owner, repositoryName);
            return MapToGitHubRelease(octo);
        }
        catch (Octokit.NotFoundException)
        {
            return null!;
        }
        catch (RateLimitExceededException ex)
        {
            _logger.LogWarning("Rate limit exceeded when fetching latest release for {Owner}/{Repo}. Reset at: {ResetTime}", owner, repositoryName, ex.Reset);
            return null!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get latest release for {Owner}/{Repo}", owner, repositoryName);
            throw;
        }
    }

    /// <summary>
    /// Gets a specific release by tag for the specified repository.
    /// </summary>
    /// <param name="owner">The repository owner.</param>
    /// <param name="repositoryName">The repository name.</param>
    /// <param name="tag">The release tag.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The <see cref="GitHubRelease"/> with the specified tag or null if not found.</returns>
    public async Task<GitHubRelease> GetReleaseByTagAsync(
        string owner,
        string repositoryName,
        string tag,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var octo = await _gitHubClient.Repository.Release.Get(owner, repositoryName, tag);
            return MapToGitHubRelease(octo);
        }
        catch (Octokit.NotFoundException)
        {
            _logger.LogDebug("Release with tag '{Tag}' not found for {Owner}/{Repo}", tag, owner, repositoryName);
            return null!;
        }
        catch (RateLimitExceededException ex)
        {
            _logger.LogWarning("Rate limit exceeded when fetching release by tag '{Tag}' for {Owner}/{Repo}. Reset at: {ResetTime}", tag, owner, repositoryName, ex.Reset);
            return null!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get release by tag '{Tag}' for {Owner}/{Repo}", tag, owner, repositoryName);
            throw;
        }
    }

    /// <summary>
    /// Gets all releases for the specified repository.
    /// </summary>
    /// <param name="owner">The repository owner.</param>
    /// <param name="repo">The repository name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task with the releases.</returns>
    public async Task<IEnumerable<GitHubRelease>> GetReleasesAsync(
        string owner,
        string repo,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var releases = await _gitHubClient.Repository.Release.GetAll(owner, repo);
            return releases.Select(MapToGitHubRelease);
        }
        catch (RateLimitExceededException ex)
        {
            _logger.LogWarning("Rate limit exceeded when fetching releases for {Owner}/{Repo}. Reset at: {ResetTime}", owner, repo, ex.Reset);
            return Enumerable.Empty<GitHubRelease>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get releases for {Owner}/{Repo}", owner, repo);
            throw;
        }
    }

    /// <summary>
    /// Gets workflow runs for the specified repository.
    /// </summary>
    /// <param name="owner">The repository owner.</param>
    /// <param name="repo">The repository name.</param>
    /// <param name="perPage">Number of runs per page.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of <see cref="GitHubWorkflowRun"/>.</returns>
    public async Task<IEnumerable<GitHubWorkflowRun>> GetWorkflowRunsForRepositoryAsync(
        string owner,
        string repo,
        int perPage = 5,
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthenticated)
        {
            _logger.LogError("Authentication required for workflow runs");
            throw new InvalidOperationException(
                "GitHub authentication required. Configure token in settings.");
        }

        try
        {
            var options = new ApiOptions { PageSize = perPage };
            var request = new WorkflowRunsRequest();
            var runs = await _gitHubClient.Actions.Workflows.Runs.List(owner, repo, request, options);
            return runs.WorkflowRuns.Select(MapToGitHubWorkflowRun);
        }
        catch (RateLimitExceededException ex)
        {
            _logger.LogWarning("Rate limit exceeded when fetching workflow runs for {Owner}/{Repo}. Reset at: {ResetTime}", owner, repo, ex.Reset);
            return Enumerable.Empty<GitHubWorkflowRun>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get workflow runs for {Owner}/{Repo}", owner, repo);
            return Enumerable.Empty<GitHubWorkflowRun>();
        }
    }

    /// <summary>
    /// Gets artifacts for a workflow run.
    /// </summary>
    /// <param name="owner">The repository owner.</param>
    /// <param name="repo">The repository name.</param>
    /// <param name="runId">The workflow run ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of <see cref="GitHubArtifact"/>.</returns>
    public async Task<IEnumerable<GitHubArtifact>> GetArtifactsForWorkflowRunAsync(
        string owner,
        string repo,
        long runId,
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthenticated)
        {
            _logger.LogError("Authentication required for artifacts");
            throw new InvalidOperationException(
                "GitHub authentication required. Configure token in settings.");
        }

        try
        {
            var artifacts = await _gitHubClient.Actions.Artifacts.ListWorkflowArtifacts(owner, repo, runId);
            return artifacts.Artifacts.Select(MapToGitHubArtifact);
        }
        catch (RateLimitExceededException ex)
        {
            _logger.LogWarning("Rate limit exceeded when fetching artifacts for workflow run {RunId}. Reset at: {ResetTime}", runId, ex.Reset);
            return Enumerable.Empty<GitHubArtifact>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get artifacts for workflow run {RunId}", runId);
            return Enumerable.Empty<GitHubArtifact>();
        }
    }

    /// <summary>
    /// Sets the authentication token for the GitHub client.
    /// </summary>
    /// <param name="token">The authentication token.</param>
    public void SetAuthenticationToken(SecureString token)
    {
        if (token == null || token.Length == 0)
        {
            throw new ArgumentException("Token cannot be null or empty.", nameof(token));
        }

        IntPtr tokenPtr = IntPtr.Zero;
        try
        {
            tokenPtr = Marshal.SecureStringToGlobalAllocUnicode(token);
            string tokenString = Marshal.PtrToStringUni(tokenPtr) ?? throw new InvalidOperationException("Failed to convert secure token to string.");

            if (_gitHubClient is GitHubClient concreteClient)
            {
                concreteClient.Credentials = new Credentials(tokenString);
                _logger.LogInformation(
                    "GitHub authentication token set successfully. IsAuthenticated: {IsAuth}, Type: {AuthType}",
                    concreteClient.Credentials != Credentials.Anonymous,
                    concreteClient.Credentials.AuthenticationType);
            }
            else
            {
                _logger.LogError("Failed to set GitHub token - client does not support setting credentials");
                throw new InvalidOperationException("The GitHub client does not support setting credentials.");
            }
        }
        finally
        {
            if (tokenPtr != IntPtr.Zero)
            {
                Marshal.ZeroFreeGlobalAllocUnicode(tokenPtr);
            }
        }
    }

    /// <summary>
    /// Maps an Octokit Release to our domain model.
    /// </summary>
    /// <param name="octokitRelease">The Octokit release.</param>
    /// <returns>The mapped release.</returns>
    private static GitHubRelease MapToGitHubRelease(Release octokitRelease)
    {
        return new GitHubRelease
        {
            Id = octokitRelease.Id,
            TagName = octokitRelease.TagName,
            Name = octokitRelease.Name ?? octokitRelease.TagName,
            Body = octokitRelease.Body,
            HtmlUrl = octokitRelease.HtmlUrl,
            PublishedAt = octokitRelease.PublishedAt,
            IsDraft = octokitRelease.Draft,
            IsPrerelease = octokitRelease.Prerelease,
            Assets = octokitRelease.Assets.Select(MapToGitHubReleaseAsset).ToList(),
        };
    }

    /// <summary>
    /// Maps an Octokit ReleaseAsset to our domain model.
    /// </summary>
    /// <param name="octokitAsset">The Octokit asset.</param>
    /// <returns>The mapped asset.</returns>
    private static GitHubReleaseAsset MapToGitHubReleaseAsset(ReleaseAsset octokitAsset)
    {
        return new GitHubReleaseAsset
        {
            Id = octokitAsset.Id,
            Name = octokitAsset.Name,
            Size = octokitAsset.Size,
            BrowserDownloadUrl = octokitAsset.BrowserDownloadUrl,
            ContentType = octokitAsset.ContentType,
        };
    }

    /// <summary>
    /// Maps an Octokit WorkflowRun to our domain model.
    /// </summary>
    /// <param name="octokitRun">The Octokit workflow run.</param>
    /// <returns>The mapped workflow run.</returns>
    private static GitHubWorkflowRun MapToGitHubWorkflowRun(WorkflowRun octokitRun)
    {
        return new GitHubWorkflowRun
        {
            Id = octokitRun.Id,
            RunNumber = (int)octokitRun.RunNumber,
            Workflow = new GitHubWorkflow
            {
                Id = octokitRun.WorkflowId,
                Name = "Unknown", // Workflow name not directly available in WorkflowRun; would need separate API call to get workflow details
            },
            CreatedAt = octokitRun.CreatedAt,
        };
    }

    /// <summary>
    /// Maps an Octokit Artifact to our domain model.
    /// </summary>
    /// <param name="octokitArtifact">The Octokit artifact.</param>
    /// <returns>The mapped artifact.</returns>
    private static GitHubArtifact MapToGitHubArtifact(Artifact octokitArtifact)
    {
        return new GitHubArtifact
        {
            Id = octokitArtifact.Id,
            Name = octokitArtifact.Name ?? string.Empty,
            SizeInBytes = octokitArtifact.SizeInBytes,
            CreatedAt = octokitArtifact.CreatedAt,
            ExpiresAt = octokitArtifact.ExpiresAt,
        };
    }
}
