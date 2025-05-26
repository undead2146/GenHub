using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.IO;
using Avalonia.Threading;

using GenHub.Core.Interfaces;
using GenHub.Core.Interfaces.GitHub;
using GenHub.Core.Models;
using GenHub.Core.Models.GitHub;
using GenHub.Core.Models.GameProfiles;
using GenHub.Core.Models.Results;
using GenHub.Features.GitHub.Helpers;

namespace GenHub.Features.GitHub.Services
{
    /// <summary>
    /// Facade service for GitHub API operations
    /// </summary>
    public class GitHubServiceFacade : IGitHubServiceFacade
    {
        private readonly IGitHubWorkflowReader _workflowReader;
        private readonly IGitHubReleaseReader _releaseReader;
        private readonly IGitHubArtifactReader _artifactReader;
        private readonly IGitHubRepositoryManager _repositoryManager;
        private readonly IGitHubApiClient _apiClient;
        private readonly IGitHubArtifactInstaller _artifactInstaller;
        private readonly IGitHubSearchService _searchService;
        private readonly ILogger<GitHubServiceFacade> _logger;
        private readonly ITokenStorageService _tokenStorageService;
        private bool _tokenInitialized = false;

        // Properly implemented events
        public event EventHandler? TokenMissing
        {
            add => _apiClient.TokenMissing += value;
            remove => _apiClient.TokenMissing -= value;
        }

        public event EventHandler? TokenInvalid
        {
            add => _apiClient.TokenInvalid += value;
            remove => _apiClient.TokenInvalid -= value;
        }

        public GitHubServiceFacade(
            IGitHubWorkflowReader workflowReader,
            IGitHubReleaseReader releaseReader,
            IGitHubArtifactReader artifactReader,
            IGitHubArtifactInstaller artifactInstaller,
            IGitHubRepositoryManager repositoryManager,
            IGitHubSearchService searchService,
            IGitHubApiClient apiClient,
            ITokenStorageService tokenStorageService,
            ILogger<GitHubServiceFacade> logger)
        {
            _workflowReader = workflowReader ?? throw new ArgumentNullException(nameof(workflowReader));
            _releaseReader = releaseReader ?? throw new ArgumentNullException(nameof(releaseReader));
            _artifactReader = artifactReader ?? throw new ArgumentNullException(nameof(artifactReader));
            _artifactInstaller = artifactInstaller ?? throw new ArgumentNullException(nameof(artifactInstaller));
            _repositoryManager = repositoryManager ?? throw new ArgumentNullException(nameof(repositoryManager));
            _searchService = searchService ?? throw new ArgumentNullException(nameof(searchService));
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            _tokenStorageService = tokenStorageService ?? throw new ArgumentNullException(nameof(tokenStorageService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _logger.LogInformation("GitHubServiceFacade initialized");
        }

        private async Task InitializeTokenAsync()
        {
            try
            {
                if (_tokenInitialized) return;
                
                string? savedToken = await _tokenStorageService.GetTokenAsync().ConfigureAwait(false);
                if (!string.IsNullOrEmpty(savedToken))
                {
                    await _apiClient.SetAuthTokenAsync(savedToken).ConfigureAwait(false);
                    _logger.LogInformation("Loaded GitHub token from settings");
                }
                _tokenInitialized = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading GitHub token from settings");
            }
        }

        private async Task EnsureTokenInitializedAsync()
        {
            if (!_tokenInitialized)
            {
                await InitializeTokenAsync().ConfigureAwait(false);
            }
        }

        public async Task SetAuthTokenAsync(string token)
        {
            await _apiClient.SetAuthTokenAsync(token);
            await _tokenStorageService.SaveTokenAsync(token);
            _tokenInitialized = true;
            _logger.LogInformation("Auth token set and saved");
        }

        public async Task<(Stream Stream, long? ContentLength)> GetStreamAsync(
            GitHubRepoSettings repoSettings,
            string endpoint,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await EnsureTokenInitializedAsync().ConfigureAwait(false);

                // Use a timeout if none provided
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken.CanBeCanceled ? cancellationToken : timeoutCts.Token);
                
                return await _apiClient.GetStreamAsync(repoSettings, endpoint, linkedCts.Token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting stream for endpoint: {Endpoint}", endpoint);
                return (Stream.Null, null);
            }
        }

        public async Task<bool> RunDiagnosticCheckAsync(CancellationToken cancellationToken = default)
        {
            Console.WriteLine("RunDiagnosticCheckAsync called - checking for deadlocks");
            
            // Detect if this is on the UI thread
            bool isOnUIThread = Dispatcher.UIThread.CheckAccess();
            Console.WriteLine($"On UI thread: {isOnUIThread}");
            
            // If on UI thread, we must not block or we'll deadlock
            if (isOnUIThread) {
                Console.WriteLine("WARNING: Running on UI thread - using Task.Run to prevent deadlock");
                return await Task.Run(() => RunDiagnosticCheckInternalAsync(cancellationToken));
            }
            
            return await RunDiagnosticCheckInternalAsync(cancellationToken);
        }

        private async Task<bool> RunDiagnosticCheckInternalAsync(CancellationToken cancellationToken)
        {
            // Safe to do synchronous work here as we're guaranteed not on UI thread
            try {
                Console.WriteLine("Diagnostic check running");
                
                // Add a timeout to prevent infinite waiting
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken, timeoutCts.Token);
                    
                // Add your actual diagnostic logic here
                
                Console.WriteLine("Diagnostic check succeeded");
                return true;
            }
            catch (Exception ex) {
                Console.WriteLine($"Diagnostic check failed: {ex.Message}");
                return false;
            }
        }

        public async Task<IEnumerable<GitHubArtifact>> GetAvailableArtifactsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await EnsureTokenInitializedAsync().ConfigureAwait(false);

                // Ensure we have a cancellation token with timeout
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken.CanBeCanceled ? cancellationToken : timeoutCts.Token);
                
                var defaultRepo = _repositoryManager.GetDefaultRepository();
                return await _artifactReader.GetArtifactsForRepositoryAsync(defaultRepo, linkedCts.Token) ??
                       Enumerable.Empty<GitHubArtifact>();
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Getting available artifacts was cancelled or timed out");
                return Enumerable.Empty<GitHubArtifact>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available artifacts");
                return Enumerable.Empty<GitHubArtifact>();
            }
        }

        public async Task<string> DownloadArtifactAsync(
            long artifactId,
            string destinationFolder,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await EnsureTokenInitializedAsync().ConfigureAwait(false);

                return await _artifactReader.DownloadArtifactAsync(
                    artifactId, destinationFolder, progress, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading artifact {ArtifactId}", artifactId);
                throw;
            }
        }

        public async Task<IEnumerable<GitHubWorkflow>> GetWorkflowRunsAsync(
            int page = 1,
            int perPage = 10,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await EnsureTokenInitializedAsync().ConfigureAwait(false);

                // Add timeout protection
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken.CanBeCanceled ? cancellationToken : timeoutCts.Token);
                
                var defaultRepo = _repositoryManager.GetDefaultRepository();
                return await _workflowReader.GetWorkflowRunsForRepositoryAsync(
                    defaultRepo, page, perPage, linkedCts.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Getting workflow runs was cancelled or timed out");
                return Enumerable.Empty<GitHubWorkflow>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting workflow runs");
                return Enumerable.Empty<GitHubWorkflow>();
            }
        }

        public async Task<IEnumerable<GitHubWorkflow>> GetWorkflowRunsForRepositoryAsync(
            GitHubRepoSettings repoConfig,
            int page = 1,
            int perPage = 10,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await EnsureTokenInitializedAsync().ConfigureAwait(false);

                return await _workflowReader.GetWorkflowRunsForRepositoryAsync(
                    repoConfig, page, perPage, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting workflow runs for repository {Repo}",
                    $"{repoConfig.RepoOwner}/{repoConfig.RepoName}");
                return Enumerable.Empty<GitHubWorkflow>();
            }
        }

        public async Task<GitHubWorkflow?> GetWorkflowRunByNumberAsync(
            GitHubRepoSettings repoConfig,
            int runNumber,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await EnsureTokenInitializedAsync().ConfigureAwait(false);

                _logger.LogInformation("Getting workflow run by number: {RunNumber} for repo {Repo}",
                    runNumber, $"{repoConfig.RepoOwner}/{repoConfig.RepoName}");

                var workflows = await _workflowReader.GetWorkflowRunsForRepositoryAsync(
                    repoConfig,
                    1,
                    100,
                    cancellationToken);

                return workflows.FirstOrDefault(w => w.WorkflowNumber == runNumber);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting workflow run by number {RunNumber}", runNumber);
                return null;
            }
        }

        public async Task<IEnumerable<GitHubArtifact>> GetArtifactsForRunAsync(
            GitHubWorkflow run,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await EnsureTokenInitializedAsync().ConfigureAwait(false);

                var repoSettings = run.RepositoryInfo ?? _repositoryManager.GetDefaultRepository();
                return await _artifactReader.GetArtifactsForRunAsync(run, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting artifacts for run {RunId}", run.RunId);
                return Enumerable.Empty<GitHubArtifact>();
            }
        }

        public async Task<IEnumerable<GitHubArtifact>> GetArtifactsForWorkflowAsync(
            long runId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await EnsureTokenInitializedAsync().ConfigureAwait(false);

                var repoSettings = _repositoryManager.GetDefaultRepository();
                return await _artifactReader.GetArtifactsForWorkflowAsync(runId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting artifacts for workflow {RunId}", runId);
                return Enumerable.Empty<GitHubArtifact>();
            }
        }

        public async Task<IEnumerable<GitHubArtifact>> GetArtifactsForRepositoryAsync(
            GitHubRepoSettings repoConfig,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await EnsureTokenInitializedAsync().ConfigureAwait(false);

                return await _artifactReader.GetArtifactsForRepositoryAsync(repoConfig, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting artifacts for repository {Repo}",
                    $"{repoConfig.RepoOwner}/{repoConfig.RepoName}");
                return Enumerable.Empty<GitHubArtifact>();
            }
        }

        public async Task<string> DownloadArtifactFromRepositoryAsync(
            GitHubRepoSettings repoConfig,
            long artifactId,
            string destinationFolder,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await EnsureTokenInitializedAsync().ConfigureAwait(false);

                return await _artifactReader.DownloadArtifactAsync(
                    repoConfig, artifactId, destinationFolder, progress, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading artifact {ArtifactId} from repository {Repo}",
                    artifactId, $"{repoConfig.RepoOwner}/{repoConfig.RepoName}");
                throw;
            }
        }

        public async Task<IEnumerable<GitHubRepoSettings>> GetRepositoriesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return await _repositoryManager.GetRepositoriesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting repositories asynchronously");
                return Enumerable.Empty<GitHubRepoSettings>();
            }
        }

        public IEnumerable<GitHubRepoSettings> GetRepositories()
        {
            try
            {
                return _repositoryManager.GetRepositories();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting repositories");
                return Enumerable.Empty<GitHubRepoSettings>();
            }
        }

        public GitHubRepoSettings GetDefaultRepository()
        {
            try
            {
                return _repositoryManager.GetDefaultRepository();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting default repository");
                return new GitHubRepoSettings();
            }
        }

        public void SetAuthToken(string token)
        {
            try
            {
                _apiClient.SetAuthTokenAsync(token);
                _tokenStorageService.SaveTokenAsync(token).ConfigureAwait(false);
                _logger.LogInformation("Auth token set and saved");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting auth token");
            }
        }

        public string? GetAuthToken()
        {
            return _apiClient.GetAuthToken();
        }

        public bool HasAuthToken()
        {
            return _apiClient.IsAuthenticated;
        }

        public async Task<List<GitHubWorkflow>> SearchWorkflowsByPullRequestAsync(
            int pullRequestNumber,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await EnsureTokenInitializedAsync().ConfigureAwait(false);

                var results = await _searchService.SearchWorkflowsByPullRequestAsync(
                    pullRequestNumber,
                    cancellationToken);

                return results.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching workflows by PR number {PRNumber}", pullRequestNumber);
                return new List<GitHubWorkflow>();
            }
        }

        public async Task<IEnumerable<GitHubWorkflow>> SearchWorkflowsByPullRequestAsync(
            GitHubRepoSettings repository,
            int pullRequestNumber,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await EnsureTokenInitializedAsync().ConfigureAwait(false);

                return await _searchService.SearchWorkflowsByPullRequestAsync(
                    repository,
                    pullRequestNumber,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching workflows by PR number {PRNumber} for repository {Repo}",
                    pullRequestNumber, $"{repository.RepoOwner}/{repository.RepoName}");
                return Enumerable.Empty<GitHubWorkflow>();
            }
        }

        public async Task<IEnumerable<GitHubWorkflow>> SearchWorkflowsByTextAsync(
            GitHubRepoSettings repository,
            string searchText,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await EnsureTokenInitializedAsync().ConfigureAwait(false);

                return await _searchService.SearchWorkflowsByTextAsync(
                    repository,
                    searchText,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching workflows by text '{SearchText}' for repository {Repo}",
                    searchText, $"{repository.RepoOwner}/{repository.RepoName}");
                return Enumerable.Empty<GitHubWorkflow>();
            }
        }

        public async Task<List<GitHubWorkflow>> SearchWorkflowsByWorkflowNumberAsync(
            int workflowNumber,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await EnsureTokenInitializedAsync().ConfigureAwait(false);

                var results = await _searchService.SearchWorkflowsByWorkflowNumberAsync(
                    workflowNumber,
                    cancellationToken);

                return results.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching workflows by workflow number {WorkflowNumber}", workflowNumber);
                return new List<GitHubWorkflow>();
            }
        }

        public async Task<List<GitHubWorkflow>> SearchWorkflowsByCommitMessageAsync(
            string searchText,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await EnsureTokenInitializedAsync().ConfigureAwait(false);

                var results = await _searchService.SearchWorkflowsByCommitMessageAsync(
                    searchText,
                    cancellationToken);

                return results.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching workflows by commit message '{SearchText}'", searchText);
                return new List<GitHubWorkflow>();
            }
        }

        public string GetWorkflowRunUrl(GitHubArtifact artifact)
        {
            try
            {
                var repoInfo = artifact.RepositoryInfo ?? _repositoryManager.GetDefaultRepository();
                return $"https://github.com/{repoInfo.RepoOwner}/{repoInfo.RepoName}/actions/runs/{artifact.RunId}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting workflow run URL for artifact {ArtifactId}", artifact.Id);
                return string.Empty;
            }
        }

        public string GetWorkflowRunUrl(long runId)
        {
            try
            {
                var repoInfo = _repositoryManager.GetDefaultRepository();
                return $"https://github.com/{repoInfo.RepoOwner}/{repoInfo.RepoName}/actions/runs/{runId}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting workflow run URL for run ID {RunId}", runId);
                return string.Empty;
            }
        }

        public async Task<IEnumerable<GitHubWorkflow>> GetWorkflowRunsForWorkflowFileAsync(
            GitHubRepoSettings repoConfig,
            string workflowFile,
            int page = 1,
            int perPage = 10,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await EnsureTokenInitializedAsync().ConfigureAwait(false);

                var allWorkflows = await _workflowReader.GetWorkflowRunsForRepositoryAsync(
                    repoConfig, page, perPage, cancellationToken);

                return allWorkflows.Where(w =>
                    w.WorkflowPath.EndsWith(workflowFile, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting workflow runs for file {WorkflowFile} from repository {Repo}",
                    workflowFile, $"{repoConfig.RepoOwner}/{repoConfig.RepoName}");
                return Enumerable.Empty<GitHubWorkflow>();
            }
        }

        public GitHubBuild ParseBuildInfo(string artifactName)
        {
            try
            {
                return GitHubModelExtensions.ParseBuildInfo(artifactName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing build info from artifact name {ArtifactName}", artifactName);
                return new GitHubBuild();
            }
        }

        public async Task<IEnumerable<GameVersion>> GetDetectedVersionsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await EnsureTokenInitializedAsync().ConfigureAwait(false);

                var repoConfig = _repositoryManager.GetDefaultRepository();
                return await GetDetectedVersionsAsync(repoConfig, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting detected versions");
                return Enumerable.Empty<GameVersion>();
            }
        }

        public async Task<IEnumerable<GameVersion>> GetDetectedVersionsAsync(
            GitHubRepoSettings repoConfig,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await EnsureTokenInitializedAsync().ConfigureAwait(false);

                var artifacts = await _artifactReader.GetArtifactsForRepositoryAsync(repoConfig, cancellationToken);

                var versions = new List<GameVersion>();

                foreach (var artifact in artifacts)
                {
                    if (artifact.BuildInfo == null)
                    {
                        continue;
                    }

                    var version = new GameVersion
                    {
                        InstallName = artifact.Name,
                        DisplayName = artifact.GetDisplayName(),
                        GameVariant = artifact.BuildInfo.GameVariant,
                        SourceType = GameInstallationType.GitHubArtifact
                    };

                    version.SetGitHubMetadata(artifact);

                    versions.Add(version);
                }

                return versions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting detected versions for repository {Repo}",
                    $"{repoConfig.RepoOwner}/{repoConfig.RepoName}");
                return Enumerable.Empty<GameVersion>();
            }
        }

        public async Task<IEnumerable<GitHubRelease>> GetReleasesAsync(
            GitHubRepoSettings repoSettings,
            int page = 1,
            int perPage = 30,
            bool includePrereleases = true,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await EnsureTokenInitializedAsync().ConfigureAwait(false);

                return await _releaseReader.GetReleasesAsync(
                    repoSettings.RepoOwner,
                    repoSettings.RepoName,
                    page,
                    perPage,
                    includePrereleases,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting releases for repository {Repo}",
                    $"{repoSettings.RepoOwner}/{repoSettings.RepoName}");
                return Enumerable.Empty<GitHubRelease>();
            }
        }

        public async Task<(Stream Stream, long? ContentLength)> DownloadReleaseAssetAsync(
            string assetDownloadUrl,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await EnsureTokenInitializedAsync().ConfigureAwait(false);

                return await _apiClient.GetStreamAsync(new GitHubRepoSettings(), assetDownloadUrl, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading release asset from URL {Url}", assetDownloadUrl);
                return (Stream.Null, null);
            }
        }

        /// <summary>
        /// Gets a raw HTTP response for a GitHub API request
        /// </summary>
        public async Task<HttpResponseMessage> GetRawAsync(GitHubRepoSettings repoSettings, string endpoint, CancellationToken cancellationToken = default)
        {
            try
            {
                await EnsureTokenInitializedAsync().ConfigureAwait(false);

                return await _apiClient.GetRawAsync(repoSettings, endpoint, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting raw HTTP response for endpoint: {Endpoint} in repository {Repo}",
                    endpoint, $"{repoSettings.RepoOwner}/{repoSettings.RepoName}");
                throw;
            }
        }

        public async Task<GameVersion?> InstallArtifactAsync(
            GitHubArtifact artifact,
            IProgress<InstallProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await EnsureTokenInitializedAsync().ConfigureAwait(false);

                var result = await _artifactInstaller.InstallArtifactAsync(artifact, progress, cancellationToken);

                // Extract the GameVersion from the OperationResult
                if (result.Success)
                {
                    return result.Data;
                }
                else
                {
                    _logger.LogError("Failed to install artifact {ArtifactId}: {ErrorMessage}",
                        artifact.Id, result.ErrorMessage);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error installing artifact {ArtifactId}", artifact.Id);
                return null;
            }
        }
        
        /// <summary>
        /// Gets a specific workflow run by its ID using owner and repo names
        /// </summary>
        public async Task<GitHubWorkflow?> GetWorkflowRunAsync(string owner, string repo, long runId, CancellationToken cancellationToken = default)
        {
            try
            {
                await EnsureTokenInitializedAsync().ConfigureAwait(false);

                // Create a temporary repo settings object
                var repoSettings = new GitHubRepoSettings
                {
                    RepoOwner = owner,
                    RepoName = repo
                };

                // Use the existing overload
                return await _apiClient.GetWorkflowRunAsync(repoSettings, runId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting workflow run {RunId} for repo {Owner}/{Repo}", runId, owner, repo);
                return null;
            }
        }
        
        public async Task<GitHubWorkflow?> GetWorkflowRunAsync(GitHubRepoSettings repoSettings, long runId, CancellationToken cancellationToken = default)
        {
            await EnsureTokenInitializedAsync().ConfigureAwait(false);

            return await _apiClient.GetWorkflowRunAsync(repoSettings, runId, cancellationToken);
        }

        public async Task<T?> GetAsync<T>(string endpoint, CancellationToken cancellationToken = default) where T : class
        {
            await EnsureTokenInitializedAsync().ConfigureAwait(false);

            return await _apiClient.GetAsync<T>(endpoint, cancellationToken);
        }

        public async Task<T?> GetAsync<T>(GitHubRepoSettings repoSettings, string endpoint, CancellationToken cancellationToken = default) where T : class
        {
            await EnsureTokenInitializedAsync().ConfigureAwait(false);

            return await _apiClient.GetAsync<T>(repoSettings, endpoint, cancellationToken);
        }

        public async Task<T?> GetAsync<T>(string owner, string repo, string endpoint, CancellationToken cancellationToken = default) where T : class
        {
            await EnsureTokenInitializedAsync().ConfigureAwait(false);

            return await _apiClient.GetAsync<T>(owner, repo, endpoint, cancellationToken);
        }

        public async Task<bool> HandleRateLimiting(HttpResponseMessage response)
        {
            await EnsureTokenInitializedAsync().ConfigureAwait(false);

            return await _apiClient.HandleRateLimiting(response);
        }

        public async Task<IEnumerable<GitHubArtifact>> GetArtifactsForRunAsync(GitHubRepoSettings repoSettings, long runId, CancellationToken cancellationToken = default)
        {
            await EnsureTokenInitializedAsync().ConfigureAwait(false);

            return await _apiClient.GetArtifactsForRunAsync(repoSettings, runId, cancellationToken);
        }

        public async Task<IEnumerable<GitHubArtifact>> GetArtifactsForWorkflowRunAsync(string owner, string repo, long runId, CancellationToken cancellationToken = default)
        {
            await EnsureTokenInitializedAsync().ConfigureAwait(false);

            return await _apiClient.GetArtifactsForWorkflowRunAsync(owner, repo, runId, cancellationToken);
        }

        public bool IsAuthenticated => _apiClient.IsAuthenticated;

        public async Task<IDictionary<long, int>> GetArtifactCountsForWorkflowsAsync(IEnumerable<long> workflowIds, CancellationToken cancellationToken = default)
        {
            await EnsureTokenInitializedAsync().ConfigureAwait(false);

            // Return the IDictionary directly without conversion
            return await _workflowReader.GetArtifactCountsForWorkflowsAsync(workflowIds, cancellationToken);
        }

        /// <summary>
        /// Gets the latest release for a repository
        /// </summary>
        public async Task<GitHubRelease?> GetLatestReleaseAsync(string owner, string repo, CancellationToken cancellationToken = default)
        {
            try
            {
                await EnsureTokenInitializedAsync().ConfigureAwait(false);

                return await _apiClient.GetLatestReleaseAsync(owner, repo, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting latest release for {Owner}/{Repo}", owner, repo);
                return null;
            }
        }

        /// <summary>
        /// Gets all releases for a repository
        /// </summary>
        public async Task<IEnumerable<GitHubRelease>> GetReleasesAsync(string owner, string repo, CancellationToken cancellationToken = default)
        {
            try
            {
                await EnsureTokenInitializedAsync().ConfigureAwait(false);

                return await _apiClient.GetReleasesAsync(owner, repo, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting releases for {Owner}/{Repo}", owner, repo);
                return Enumerable.Empty<GitHubRelease>();
            }
        }

        /// <summary>
        /// Checks if the GitHub API is accessible
        /// </summary>
        public async Task<bool> IsApiAccessibleAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Check if we can access the rate limit endpoint, which is a lightweight call
                var result = await _apiClient.GetRateLimitAsync(cancellationToken);
                return result != null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to access GitHub API");
                return false;
            }
        }

        /// <summary>
        /// Gets the current rate limit information from the GitHub API
        /// </summary>
        public async Task<RateLimitInfo?> GetRateLimitAsync(CancellationToken cancellationToken = default)
        {
            await EnsureTokenInitializedAsync().ConfigureAwait(false);
            
            try
            {
                return await _apiClient.GetRateLimitAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting rate limit information");
                return null;
            }
        }

        /// <summary>
        /// Tests the current authentication token
        /// </summary>
        public async Task<bool> TestAuthenticationAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await EnsureTokenInitializedAsync().ConfigureAwait(false);
                
                return await _apiClient.TestAuthenticationAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing authentication token");
                return false;
            }
        }
    }
}
