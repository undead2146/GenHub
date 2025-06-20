using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

using GenHub.Core.Interfaces;
using GenHub.Core.Interfaces.Repositories;
using GenHub.Core.Interfaces.GameVersions;
using GenHub.Core.Models;
using GenHub.Core.Models.GitHub;
using GenHub.Core.Models.GameProfiles;
using GenHub.Core.Models.Results;
using GenHub.Core.Models.SourceMetadata;
using GenHub.Core.Interfaces.GitHub;
using GenHub.Core.Models.GitHub.Exceptions;

namespace GenHub.Features.GitHub.Services
{
    /// <summary>
    /// Service for GitHub artifact operations with enhanced error handling and validation
    /// </summary>
    public class GitHubArtifactReader : IGitHubArtifactReader
    {
        private readonly IGitHubApiClient _apiClient;
        private readonly IGitHubCachingRepository _dataRepository;
        private readonly ILogger<GitHubArtifactReader> _logger;
        private readonly IGameVersionInstaller _versionInstaller;
        private readonly IGitHubWorkflowReader _workflowService;
        private readonly IGameVersionRepository _gameVersionRepository;
        private readonly IGitHubRepositoryManager _repoService;

        public GitHubArtifactReader(
            IGitHubApiClient apiClient,
            IGitHubCachingRepository dataRepository,
            ILogger<GitHubArtifactReader> logger,
            IGameVersionInstaller versionInstaller,
            IGitHubWorkflowReader workflowService,
            IGameVersionRepository gameVersionRepository,
            IGitHubRepositoryManager repoService)
        {
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            _dataRepository = dataRepository ?? throw new ArgumentNullException(nameof(dataRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _versionInstaller = versionInstaller ?? throw new ArgumentNullException(nameof(versionInstaller));
            _workflowService = workflowService ?? throw new ArgumentNullException(nameof(workflowService));
            _gameVersionRepository = gameVersionRepository ?? throw new ArgumentNullException(nameof(gameVersionRepository));
            _repoService = repoService ?? throw new ArgumentNullException(nameof(repoService));

            _logger.LogInformation("GitHubArtifactReader initialized");
        }

        /// <summary>
        /// Gets artifacts for a specific workflow run with comprehensive validation
        /// </summary>
        public async Task<IEnumerable<GitHubArtifact>> GetArtifactsForRunAsync(
            GitHubWorkflow run,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Validate input parameters
                if (run == null || run.RunId <= 0)
                {
                    _logger.LogWarning("Invalid workflow run - Run is null: {IsNull}, RunId: {RunId}", 
                        run == null, run?.RunId ?? 0);
                    return Enumerable.Empty<GitHubArtifact>();
                }

                _logger.LogDebug("Starting artifact fetch for run {RunId}, WorkflowNumber: {WorkflowNumber}, Name: '{Name}'", 
                    run.RunId, run.WorkflowNumber, run.Name);

                // Validate workflow properties and log warnings for invalid data
                ValidateWorkflowProperties(run);

                // Ensure repository info exists
                var repo = EnsureRepositoryInfo(run);
                _logger.LogDebug("Using repository: {Owner}/{Repo}", repo.RepoOwner, repo.RepoName);

                // Check cache first
                var cachedArtifacts = await TryGetCachedArtifacts(repo, run.RunId, cancellationToken);
                if (cachedArtifacts != null)
                {
                    _logger.LogDebug("Using {Count} cached artifacts for run {RunId}", cachedArtifacts.Count(), run.RunId);
                    return cachedArtifacts;
                }

                // Fetch from API if not cached
                return await FetchArtifactsFromApi(repo, run, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting artifacts for run {RunId}: {ErrorMessage}",
                    run?.RunId ?? 0, ex.Message);
                return Enumerable.Empty<GitHubArtifact>();
            }
        }

        /// <summary>
        /// Gets artifacts for a specific workflow run by run ID
        /// </summary>
        public async Task<IEnumerable<GitHubArtifact>> GetArtifactsForWorkflowAsync(
            long runId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (runId <= 0)
                {
                    _logger.LogWarning("Invalid workflow runId: {RunId}", runId);
                    return Enumerable.Empty<GitHubArtifact>();
                }

                _logger.LogInformation("Getting artifacts for workflow {RunId}", runId);

                // Check cache across all repositories
                var cachedArtifacts = await TryGetCachedArtifactsFromAllRepos(runId, cancellationToken);
                if (cachedArtifacts != null)
                {
                    return cachedArtifacts;
                }

                // Fetch workflow run and get artifacts
                var run = await GetWorkflowRunAsync(runId, cancellationToken);
                if (run == null)
                {
                    _logger.LogWarning("Could not find workflow run {RunId}", runId);
                    return Enumerable.Empty<GitHubArtifact>();
                }

                return await GetArtifactsForRunAsync(run, cancellationToken) ?? Enumerable.Empty<GitHubArtifact>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting artifacts for workflow {RunId}: {ErrorMessage}", runId, ex.Message);
                return Enumerable.Empty<GitHubArtifact>();
            }
        }

        /// <summary>
        /// Get available workflow artifacts for the configured repository
        /// </summary>
        public async Task<IEnumerable<GitHubArtifact>> GetAvailableArtifactsAsync(
            CancellationToken cancellationToken = default)
        {
            return await GetArtifactsForRepositoryAsync(_repoService.GetDefaultRepository(), cancellationToken);
        }

        /// <summary>
        /// Get artifacts from a specific repository
        /// </summary>
        public async Task<IEnumerable<GitHubArtifact>> GetArtifactsForRepositoryAsync(
            GitHubRepository repoConfig,
            CancellationToken cancellationToken = default)
        {
            var runs = await _workflowService.GetWorkflowRunsForRepositoryAsync(repoConfig, cancellationToken: cancellationToken);
            var artifacts = new List<GitHubArtifact>();

            foreach (var run in runs)
            {
                try
                {
                    var runArtifacts = await GetArtifactsForRunAsync(run, cancellationToken);
                    artifacts.AddRange(runArtifacts);
                }
                catch (GitHubServiceException ex) when (ex.Message.Contains("rate limit"))
                {
                    _logger.LogWarning("Rate limit reached after loading {count} artifacts. Error: {message}", artifacts.Count, ex.Message);
                    break;
                }
            }

            return artifacts;
        }

        /// <summary>
        /// Download a specific artifact by ID using default repository
        /// </summary>
        public async Task<string> DownloadArtifactAsync(
            long artifactId,
            string destinationFolder,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var repo = _repoService.GetDefaultRepository();
            return await DownloadArtifactAsync(repo, artifactId, destinationFolder, progress, cancellationToken);
        }

        /// <summary>
        /// Download an artifact from a specific repository with comprehensive error handling
        /// </summary>
        public async Task<string> DownloadArtifactAsync(
            GitHubRepository repoConfig,
            long artifactId,
            string destinationFolder,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            // Comprehensive input validation
            ValidateDownloadParameters(repoConfig, artifactId, destinationFolder);

            Directory.CreateDirectory(destinationFolder);
            var zipPath = Path.Combine(destinationFolder, $"artifact_{artifactId}.zip");

            try
            {
                var artifactUrl = $"https://api.github.com/repos/{repoConfig.RepoOwner}/{repoConfig.RepoName}/actions/artifacts/{artifactId}/zip";
                _logger.LogInformation("Downloading from: {url}", artifactUrl);

                // Check authentication status
                var hasAuth = !string.IsNullOrEmpty(_apiClient.GetAuthToken());
                _logger.LogDebug("Authentication status: {HasAuth}", hasAuth);

                // Get response with null checking
                var response = await _apiClient.GetAsync<HttpResponseMessage>(repoConfig, artifactUrl, cancellationToken);
                ValidateApiResponse(response, artifactId, hasAuth);

                // Handle error status codes
                await HandleErrorStatusCodes(response, artifactId, repoConfig, hasAuth, cancellationToken);

                // Handle rate limiting
                bool shouldContinue = await _apiClient.HandleRateLimiting(response);
                if (!shouldContinue)
                {
                    throw new GitHubServiceException("Rate limit exceeded. Please try again later.");
                }
                
                response.EnsureSuccessStatusCode();

                // Download with progress reporting
                await DownloadWithProgress(response, zipPath, progress, cancellationToken);

                _logger.LogInformation("Successfully downloaded {bytes} bytes to {path}", new FileInfo(zipPath).Length, zipPath);
                return zipPath;
            }
            catch (GitHubServiceException)
            {
                CleanupFailedDownload(zipPath);
                throw;
            }
            catch (HttpRequestException ex)
            {
                CleanupFailedDownload(zipPath);
                _logger.LogError(ex, "HTTP error downloading artifact {ArtifactId}: {StatusCode}", 
                    artifactId, ex.StatusCode);
                
                var userMessage = GetUserFriendlyHttpErrorMessage(ex.StatusCode);
                throw new GitHubServiceException($"Failed to download artifact: {userMessage}", ex);
            }
            catch (Exception ex)
            {
                CleanupFailedDownload(zipPath);
                _logger.LogError(ex, "Unexpected error downloading artifact {ArtifactId}", artifactId);
                throw;
            }
        }

        /// <summary>
        /// Installs a GitHub artifact with comprehensive validation and error handling
        /// </summary>
        public async Task<GameVersion?> InstallArtifactAsync(
            GitHubArtifact artifact,
            IProgress<InstallProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            // Validate input and ensure repository info exists
            ValidateArtifactForInstallation(artifact);

            string tempPath = Path.Combine(Path.GetTempPath(), "GenHub", "Downloads");
            Directory.CreateDirectory(tempPath);

            try
            {
                _logger.LogInformation("Installing artifact: {ArtifactName} ({ArtifactId})", artifact.Name, artifact.Id);

                progress?.Report(new InstallProgress { Message = "Downloading artifact...", Percentage = 0 });

                // Download with progress mapping (0-40%)
                var downloadProgress = new Progress<double>(p =>
                    progress?.Report(new InstallProgress { Message = $"Downloading... {p:P0}", Percentage = p * 0.4 }));

                string downloadFile = await DownloadArtifactWithValidation(artifact, tempPath, downloadProgress, cancellationToken);

                // Install with progress mapping (40-100%)
                var installProgress = new Progress<InstallProgress>(p =>
                {
                    var adjustedPercentage = 0.4 + (p.Percentage * 0.6);
                    progress?.Report(new InstallProgress
                    {
                        Message = p.Message,
                        Percentage = adjustedPercentage,
                        TotalFiles = p.TotalFiles,
                        Stage = p.Stage
                    });
                });

                return await InstallArtifactFile(artifact, downloadFile, installProgress, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Artifact installation was cancelled: {ArtifactId}", artifact.Id);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error installing artifact {ArtifactId}: {ErrorMessage}", artifact.Id, ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Detects available game versions from GitHub artifacts
        /// </summary>
        public async Task<IEnumerable<GameVersion>> GetDetectedVersionsAsync(CancellationToken cancellationToken = default)
        {
            var artifacts = await GetAvailableArtifactsAsync(cancellationToken);
            return artifacts.Select(a => new GameVersion
            {
                Id = $"github-{a.Id}",
                Name = a.Name,
                SourceType = GameInstallationType.GitHubArtifact,
                SourceSpecificMetadata = new GitHubSourceMetadata
                {
                    AssociatedArtifact = a,
                    WorkflowDefinitionName = null,
                    WorkflowDefinitionPath = null,
                    WorkflowRunStatus = null,
                    WorkflowRunConclusion = null,
                    SourceControlBranch = null
                    // You can fill these if you have the info
                },
                IsValid = false,
                InstallDate = default
            });
        }

        /// <summary>
        /// Detects available game versions from GitHub artifacts in a specific repository
        /// </summary>
        public async Task<IEnumerable<GameVersion>> GetDetectedVersionsAsync(GitHubRepository repoConfig, CancellationToken cancellationToken = default)
        {
            try
            {
                var artifacts = await GetArtifactsForRepositoryAsync(repoConfig, cancellationToken);
                if (artifacts == null || !artifacts.Any())
                {
                    return Enumerable.Empty<GameVersion>();
                }

                return artifacts.Select(a => new GameVersion
                {
                    Id = $"github-{a.Id}",
                    Name = a.Name,
                    SourceType = GameInstallationType.GitHubArtifact,
                    SourceSpecificMetadata = new GitHubSourceMetadata
                    {
                        AssociatedArtifact = a,
                        WorkflowDefinitionName = null,
                        WorkflowDefinitionPath = null,
                        WorkflowRunStatus = null,
                        WorkflowRunConclusion = null,
                        SourceControlBranch = null
                        // You can fill these if you have the info
                    },
                    IsValid = false,
                    InstallDate = default
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting detected versions from GitHub: {ErrorMessage}", ex.Message);
                return Enumerable.Empty<GameVersion>();
            }
        }

        /// <summary>
        /// Parses build information from an artifact name
        /// </summary>
        public GitHubBuild ParseBuildInfo(string artifactName)
        {
            var info = new GitHubBuild();

            try
            {
                // Parse artifact name format: "GameVariant-Compiler-Configuration+Flags"
                string[] parts = artifactName.Split('-');
                if (parts.Length > 0)
                {
                    info.GameVariant = ParseGameVariant(parts[0]);
                }

                if (parts.Length > 1)
                {
                    ParseCompilerAndFlags(parts[1], info);
                }

                if (parts.Length > 2)
                {
                    ParseConfigurationAndFlags(parts[2], info);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error parsing build info from artifact name: {ArtifactName}", artifactName);
            }

            return info;
        }

        #region Private Helper Methods

        private void ValidateWorkflowProperties(GitHubWorkflow run)
        {
            if (run.WorkflowNumber <= 0)
            {
                _logger.LogWarning("Source workflow has invalid WorkflowNumber: {WorkflowNumber}", run.WorkflowNumber);
            }
            
            if (run.CreatedAt == DateTime.MinValue || run.CreatedAt == default)
            {
                _logger.LogWarning("Source workflow has invalid CreatedAt: {CreatedAt}", run.CreatedAt);
            }
        }

        private GitHubRepository EnsureRepositoryInfo(GitHubWorkflow run)
        {
            if (run.RepositoryInfo == null)
            {
                _logger.LogWarning("Workflow run {RunId} is missing repository info - using default repository", run.RunId);
                run.RepositoryInfo = _repoService.GetDefaultRepository();
            }

            // Create defensive copy to prevent sharing
            return new GitHubRepository
            {
                RepoOwner = run.RepositoryInfo.RepoOwner,
                RepoName = run.RepositoryInfo.RepoName,
                DisplayName = run.RepositoryInfo.DisplayName,
                Token = run.RepositoryInfo.Token,
                WorkflowFile = run.RepositoryInfo.WorkflowFile,
                Branch = run.RepositoryInfo.Branch
            };
        }

        private async Task<IEnumerable<GitHubArtifact>?> TryGetCachedArtifacts(
            GitHubRepository repo, 
            long runId, 
            CancellationToken cancellationToken)
        {
            var repoFullName = $"{repo.RepoOwner}/{repo.RepoName}";
            var cachedArtifacts = await _dataRepository.GetCachedArtifactsAsync(repoFullName, runId, cancellationToken);

            if (cachedArtifacts != null && cachedArtifacts.Any())
            {
                return cachedArtifacts;
            }

            _logger.LogDebug("No cached artifacts found, fetching from API");
            return null;
        }

        private async Task<IEnumerable<GitHubArtifact>?> TryGetCachedArtifactsFromAllRepos(
            long runId, 
            CancellationToken cancellationToken)
        {
            var repos = _repoService.GetRepositories();
            foreach (var repo in repos)
            {
                var repoFullName = $"{repo.RepoOwner}/{repo.RepoName}";
                var cachedArtifacts = await _dataRepository.GetCachedArtifactsAsync(repoFullName, runId, cancellationToken);

                if (cachedArtifacts != null && cachedArtifacts.Any())
                {
                    _logger.LogInformation("Using {Count} cached artifacts for workflow {RunId} from repo {Repo}",
                        cachedArtifacts.Count(), runId, repoFullName);
                    return cachedArtifacts;
                }
            }
            return null;
        }

        private async Task<IEnumerable<GitHubArtifact>> FetchArtifactsFromApi(
            GitHubRepository repo,
            GitHubWorkflow run,
            CancellationToken cancellationToken)
        {
            try
            {
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
                
                _logger.LogDebug("Calling API for artifacts");
                
                var artifacts = await _apiClient.GetArtifactsForRunAsync(repo, run.RunId, linkedCts.Token);

                if (artifacts == null)
                {
                    _logger.LogWarning("API returned null artifacts collection!");
                    return Enumerable.Empty<GitHubArtifact>();
                }

                return await ProcessArtifacts(artifacts.ToList(), repo, run, cancellationToken);
            }
            catch (OperationCanceledException ex) when (ex is TaskCanceledException || cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Request for artifacts for run {RunId} was cancelled or timed out", run.RunId);
                throw new GitHubServiceException($"Request for artifacts timed out. Please try again later.", ex);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogDebug("404 response - no artifacts found for run {RunId}", run.RunId);
                return Enumerable.Empty<GitHubArtifact>();
            }
        }

        private async Task<List<GitHubArtifact>> ProcessArtifacts(
            List<GitHubArtifact> artifacts,
            GitHubRepository repo,
            GitHubWorkflow run,
            CancellationToken cancellationToken)
        {
            var processedArtifacts = new List<GitHubArtifact>();
            _logger.LogDebug("Starting processing of {Count} artifacts", artifacts.Count);

            foreach (var artifact in artifacts)
            {
                if (artifact == null)
                {
                    _logger.LogWarning("Null artifact in collection, skipping");
                    continue;
                }

                ProcessSingleArtifact(artifact, repo, run);
                processedArtifacts.Add(artifact);
            }

            _logger.LogDebug("Processed {Count} artifacts successfully", processedArtifacts.Count);

            // Cache the processed artifacts
            if (processedArtifacts.Any())
            {
                var repoFullName = $"{repo.RepoOwner}/{repo.RepoName}";
                await _dataRepository.CacheArtifactsAsync(repoFullName, run.RunId, processedArtifacts, cancellationToken);
            }

            return processedArtifacts;
        }

        private void ProcessSingleArtifact(GitHubArtifact artifact, GitHubRepository repo, GitHubWorkflow run)
        {
            _logger.LogDebug("Processing artifact: ID={Id}, Name='{Name}', Size={Size}", 
                artifact.Id, artifact.Name, artifact.SizeInBytes);

            // Set repository info (defensive copy)
            artifact.RepositoryInfo = new GitHubRepository
            {
                RepoOwner = repo.RepoOwner,
                RepoName = repo.RepoName,
                DisplayName = repo.DisplayName,
                Token = repo.Token,
                WorkflowFile = repo.WorkflowFile,
                Branch = repo.Branch
            };

            // Set workflow metadata
            artifact.WorkflowId = run.WorkflowId;
            artifact.RunId = run.RunId;
            artifact.WorkflowNumber = run.WorkflowNumber;
            artifact.PullRequestNumber = run.PullRequestNumber;
            artifact.PullRequestTitle = run.PullRequestTitle;
            artifact.CommitSha = run.CommitSha;
            artifact.CommitMessage = run.CommitMessage;
            artifact.EventType = run.EventType;
            
            // Ensure CreatedAt is valid
            if (artifact.CreatedAt == DateTime.MinValue || artifact.CreatedAt == default)
            {
                _logger.LogDebug("Artifact CreatedAt was invalid, using run CreatedAt: {RunCreatedAt}", run.CreatedAt);
                artifact.CreatedAt = run.CreatedAt;
            }

            // Parse build info
            if (artifact.BuildInfo == null)
            {
                artifact.BuildInfo = ParseBuildInfo(artifact.Name);
                _logger.LogDebug("Parsed BuildInfo: {GameVariant}, {Compiler}, {Configuration}", 
                    artifact.BuildInfo?.GameVariant, artifact.BuildInfo?.Compiler, artifact.BuildInfo?.Configuration);
            }
        }

        private void ValidateDownloadParameters(GitHubRepository repoConfig, long artifactId, string destinationFolder)
        {
            if (repoConfig == null)
            {
                _logger.LogError("Repository configuration is null when downloading artifact {ArtifactId}", artifactId);
                throw new ArgumentNullException(nameof(repoConfig), "Repository configuration cannot be null");
            }

            if (string.IsNullOrEmpty(repoConfig.RepoOwner) || string.IsNullOrEmpty(repoConfig.RepoName))
            {
                _logger.LogError("Repository owner or name is null/empty. Owner: '{Owner}', Name: '{Name}'", 
                    repoConfig.RepoOwner, repoConfig.RepoName);
                throw new ArgumentException("Repository owner and name must be provided", nameof(repoConfig));
            }

            if (_apiClient == null)
            {
                _logger.LogError("API client is null when downloading artifact {ArtifactId}", artifactId);
                throw new InvalidOperationException("API client is not initialized");
            }

            if (string.IsNullOrEmpty(destinationFolder))
            {
                _logger.LogError("Destination folder is null/empty when downloading artifact {ArtifactId}", artifactId);
                throw new ArgumentException("Destination folder cannot be null or empty", nameof(destinationFolder));
            }
        }

        private void ValidateApiResponse(HttpResponseMessage? response, long artifactId, bool hasAuth)
        {
            if (response == null)
            {
                _logger.LogError("API client returned null response for artifact {ArtifactId}", artifactId);
                
                var errorMessage = hasAuth 
                    ? $"Failed to get response from GitHub API for artifact {artifactId}"
                    : "GitHub requires authentication to download build artifacts.\n\n" +
                      "Please add a GitHub personal access token in the application settings with 'actions:read' or 'workflow' permissions.\n" +
                      "This is required even for public repositories due to GitHub API limitations.";
                      
                throw new GitHubServiceException(errorMessage);
            }

            _logger.LogInformation("Download response status: {status}", response.StatusCode);
        }

        private async Task HandleErrorStatusCodes(
            HttpResponseMessage response, 
            long artifactId, 
            GitHubRepository repoConfig, 
            bool hasAuth,
            CancellationToken cancellationToken)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                await HandleForbiddenError(response, hasAuth, repoConfig, cancellationToken);
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                throw new GitHubServiceException(
                    "Unauthorized access to GitHub API.\n\n" +
                    "Please check your GitHub personal access token in the application settings.\n" +
                    "The token may be invalid, expired, or lack the required permissions.");
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                throw new GitHubServiceException(
                    $"Artifact {artifactId} not found.\n\n" +
                    "This could be due to:\n" +
                    "• Artifact has expired (GitHub artifacts expire after 90 days)\n" +
                    "• Artifact has been deleted\n" +
                    "• Incorrect repository or artifact ID\n\n" +
                    $"Repository: {repoConfig.RepoOwner}/{repoConfig.RepoName}");
            }
        }

        private async Task HandleForbiddenError(
            HttpResponseMessage response, 
            bool hasAuth, 
            GitHubRepository repoConfig,
            CancellationToken cancellationToken)
        {
            string errorContent = string.Empty;
            try
            {
                errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("403 Forbidden response details: {ErrorContent}", errorContent);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not read error response content");
            }

            if (!hasAuth)
            {
                throw new GitHubServiceException(
                    "GitHub requires authentication to download build artifacts.\n\n" +
                    "Please add a GitHub personal access token in the application settings.\n" +
                    "Required permissions: 'actions:read' or 'workflow'\n\n" +
                    "This is required even for public repositories due to GitHub API limitations.");
            }

            // Provide specific error messages based on response content
            if (errorContent.Contains("token") || errorContent.Contains("permission"))
            {
                throw new GitHubServiceException(
                    "Authentication failed with the provided token.\n\n" +
                    "Possible issues:\n" +
                    "• Token may be expired or invalid\n" +
                    "• Token lacks required permissions ('actions:read' or 'workflow')\n" +
                    "• Token may not have access to this repository\n\n" +
                    "Please check your GitHub token in the application settings.");
            }
            else if (errorContent.Contains("rate limit"))
            {
                throw new GitHubServiceException(
                    "GitHub API rate limit exceeded. Please try again later.\n\n" +
                    "Consider using a GitHub personal access token for higher rate limits.");
            }
            else
            {
                throw new GitHubServiceException(
                    $"Access forbidden to repository.\n\n" +
                    "This could be due to:\n" +
                    "• Repository is private and token lacks access\n" +
                    "• Artifact has expired or been deleted\n" +
                    "• Token permissions are insufficient\n\n" +
                    $"Repository: {repoConfig.RepoOwner}/{repoConfig.RepoName}\n" +
                    $"Error details: {errorContent}");
            }
        }

        private async Task DownloadWithProgress(
            HttpResponseMessage response,
            string zipPath,
            IProgress<double>? progress,
            CancellationToken cancellationToken)
        {
            if (response.Content == null)
            {
                _logger.LogError("Response content is null");
                throw new GitHubServiceException("Response content is null");
            }

            var total = response.Content.Headers.ContentLength ?? -1L;
            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            
            if (stream == null)
            {
                _logger.LogError("Response stream is null");
                throw new GitHubServiceException("Response stream is null");
            }

            using var fs = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None);
            var buffer = new byte[8192];
            long bytesRead = 0;
            int bytesReadThisChunk;

            while ((bytesReadThisChunk = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                await fs.WriteAsync(buffer, 0, bytesReadThisChunk, cancellationToken);
                bytesRead += bytesReadThisChunk;

                if (total > 0 && progress != null)
                {
                    progress.Report((double)bytesRead / total);
                }
            }
        }

        private void CleanupFailedDownload(string zipPath)
        {
            if (File.Exists(zipPath))
            {
                try
                {
                    File.Delete(zipPath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to cleanup partial download: {Path}", zipPath);
                }
            }
        }

        private string GetUserFriendlyHttpErrorMessage(System.Net.HttpStatusCode? statusCode)
        {
            return statusCode switch
            {
                System.Net.HttpStatusCode.Forbidden => 
                    "Access forbidden. Please check your GitHub token permissions.",
                System.Net.HttpStatusCode.Unauthorized => 
                    "Unauthorized. Please check your GitHub token.",
                System.Net.HttpStatusCode.NotFound => 
                    "Artifact not found. It may have expired or been deleted.",
                System.Net.HttpStatusCode.TooManyRequests => 
                    "Rate limit exceeded. Please try again later.",
                _ => "Network error occurred during download."
            };
        }

        private void ValidateArtifactForInstallation(GitHubArtifact artifact)
        {
            if (artifact == null)
            {
                _logger.LogError("Artifact is null in InstallArtifactAsync");
                throw new ArgumentNullException(nameof(artifact), "Artifact cannot be null");
            }

            if (artifact.RepositoryInfo == null)
            {
                _logger.LogError("Artifact {ArtifactId} has null repository info", artifact.Id);
                
                var defaultRepo = _repoService?.GetDefaultRepository();
                if (defaultRepo != null)
                {
                    _logger.LogWarning("Using default repository for artifact {ArtifactId}", artifact.Id);
                    artifact.RepositoryInfo = defaultRepo;
                }
                else
                {
                    throw new InvalidOperationException($"Artifact {artifact.Id} has no repository information and no default repository is available");
                }
            }
        }

        private async Task<string> DownloadArtifactWithValidation(
            GitHubArtifact artifact,
            string tempPath,
            IProgress<double> downloadProgress,
            CancellationToken cancellationToken)
        {
            try
            {
                var downloadFile = await DownloadArtifactAsync(
                    artifact.RepositoryInfo!,
                    artifact.Id,
                    tempPath,
                    downloadProgress,
                    cancellationToken);

                if (string.IsNullOrEmpty(downloadFile) || !File.Exists(downloadFile))
                {
                    throw new GitHubDownloadException($"Download failed - file not found: {downloadFile}");
                }

                return downloadFile;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to download artifact {ArtifactId}: {Message}", artifact.Id, ex.Message);
                throw new GitHubDownloadException($"Failed to download artifact: {ex.Message}", ex);
            }
        }

        private async Task<GameVersion?> InstallArtifactFile(
            GitHubArtifact artifact,
            string downloadFile,
            IProgress<InstallProgress> installProgress,
            CancellationToken cancellationToken)
        {
            var options = new ExtractOptions
            {
                DeleteZipAfterExtraction = true,
                CustomInstallName = $"{DateTime.Now:yyyyMMdd}_{artifact.Name}"
            };

            var result = await _versionInstaller.InstallVersionAsync(
                artifact,
                downloadFile,
                options,
                installProgress,
                cancellationToken);

            if (result.Success && result.Data != null)
            {
                var version = result.Data;
                artifact.IsInstalled = true;

                // Update artifact metadata from installed version
                UpdateArtifactFromInstalledVersion(artifact, version);
                
                return version;
            }
            else
            {
                _logger.LogError("Failed to install version: {ErrorMessage}", result.Message);
                throw new GitHubDownloadException($"Failed to install version: {result.Message}");
            }
        }

        private void UpdateArtifactFromInstalledVersion(GitHubArtifact artifact, GameVersion version)
        {
            if (version.GitHubMetadata?.AssociatedArtifact != null)
            {
                var sourceArtifact = version.GitHubMetadata.AssociatedArtifact;
                artifact.Id = sourceArtifact.Id;
                artifact.Name = sourceArtifact.Name;
                artifact.BuildInfo = sourceArtifact.BuildInfo;
                artifact.WorkflowId = sourceArtifact.WorkflowId;
                artifact.RunId = sourceArtifact.RunId;
                artifact.WorkflowNumber = sourceArtifact.WorkflowNumber;
                artifact.PullRequestNumber = sourceArtifact.PullRequestNumber;
                artifact.PullRequestTitle = sourceArtifact.PullRequestTitle;
                artifact.CommitSha = sourceArtifact.CommitSha;
                artifact.CommitMessage = sourceArtifact.CommitMessage;
                artifact.EventType = sourceArtifact.EventType;
                artifact.CreatedAt = sourceArtifact.CreatedAt;
                artifact.BuildPreset = sourceArtifact.BuildPreset;
                artifact.RepositoryInfo = sourceArtifact.RepositoryInfo;
            }
        }

        private GameVariant ParseGameVariant(string gameVariantString)
        {
            if (gameVariantString.Equals("GeneralsMD", StringComparison.OrdinalIgnoreCase))
            {
                return GameVariant.ZeroHour;
            }
            else if (Enum.TryParse<GameVariant>(gameVariantString, true, out var gameVariant))
            {
                return gameVariant;
            }
            else
            {
                return GameVariant.Unknown;
            }
        }

        private void ParseCompilerAndFlags(string compilerPart, GitHubBuild info)
        {
            if (compilerPart.Contains("+"))
            {
                string[] flagParts = compilerPart.Split('+', StringSplitOptions.RemoveEmptyEntries);
                info.Compiler = flagParts[0];

                foreach (var flag in flagParts.Skip(1))
                {
                    if (flag == "t") info.HasTFlag = true;
                    if (flag == "e") info.HasEFlag = true;
                }
            }
            else
            {
                info.Compiler = compilerPart;
            }
        }

        private void ParseConfigurationAndFlags(string configPart, GitHubBuild info)
        {
            if (configPart.Contains("+"))
            {
                string[] flagParts = configPart.Split('+', StringSplitOptions.RemoveEmptyEntries);
                info.Configuration = flagParts[0];

                foreach (var flag in flagParts.Skip(1))
                {
                    if (flag == "t") info.HasTFlag = true;
                    if (flag == "e") info.HasEFlag = true;
                }
            }
            else
            {
                info.Configuration = configPart;
            }
        }

        private async Task<GitHubWorkflow?> GetWorkflowRunAsync(long runId, CancellationToken cancellationToken)
        {
            try
            {
                if (runId <= 0)
                {
                    _logger.LogWarning("Invalid runId: {RunId}", runId);
                    return null;
                }

                // Try to find the run in all repositories
                var repositories = new List<GitHubRepository> { _repoService.GetDefaultRepository() };
                repositories.AddRange(_repoService.GetRepositories().Where(r =>
                    r.RepoOwner != repositories[0].RepoOwner ||
                    r.RepoName != repositories[0].RepoName));

                foreach (var repo in repositories)
                {
                    try
                    {
                        _logger.LogDebug("Looking for workflow run {RunId} in repository {RepoOwner}/{RepoName}",
                            runId, repo.RepoOwner, repo.RepoName);

                        var workflow = await _workflowService.GetWorkflowRunAsync(repo, runId, cancellationToken);

                        if (workflow != null)
                        {
                            workflow.RepositoryInfo = repo;
                            return workflow;
                        }
                    }
                    catch (Exception ex) when (!(ex is OperationCanceledException))
                    {
                        _logger.LogWarning(ex, "Failed to get workflow run {RunId} from repo {Repo}/{Name}",
                            runId, repo.RepoOwner, repo.RepoName);
                    }
                }

                _logger.LogWarning("Could not find workflow run {RunId} in any repository", runId);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting workflow run {RunId}", runId);
                return null;
            }
        }

        #endregion
    }
}
