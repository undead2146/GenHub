using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using GenHub.Core.Models;
using GenHub.Core.Models.GitHub;
using GenHub.Infrastructure.Caching;
using GenHub.Core.Interfaces.Caching; 
using GenHub.Core.Interfaces.GitHub;

namespace GenHub.Features.GitHub.Services
{
    /// <summary>
    /// Service for interacting with GitHub workflows
    /// </summary>
    public class GitHubWorkflowReader : IGitHubWorkflowReader
    {
        private readonly ILogger<GitHubWorkflowReader> _logger;
        private readonly IGitHubApiClient _apiClient;
        private readonly IGitHubRepositoryManager _repoService;
        private readonly ICacheService _cachingService;
        // Add a cache for workflow runs to reduce API calls
        private readonly Dictionary<string, List<GitHubWorkflow>> _workflowRunsCache = new Dictionary<string, List<GitHubWorkflow>>();
        private readonly SemaphoreSlim _cacheLock = new SemaphoreSlim(1, 1);

        public GitHubWorkflowReader(
            ILogger<GitHubWorkflowReader> logger,
            IGitHubApiClient apiClient,
            IGitHubRepositoryManager repoService,
            ICacheService cachingService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            _repoService = repoService ?? throw new ArgumentNullException(nameof(repoService));
            _cachingService = cachingService ?? throw new ArgumentNullException(nameof(cachingService));
        }

        /// <inheritdoc />
        public async Task<IEnumerable<GitHubArtifact>> GetArtifactsForWorkflowRunAsync(long runId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Getting artifacts for workflow run {RunId}", runId);

                // Get current repository
                var repo = await _repoService.GetCurrentRepositoryAsync();

                // Use caching for artifacts per workflow run
                string cacheKey = $"artifacts_{repo.RepoOwner}_{repo.RepoName}_{runId}";
                string cacheDir = Path.Combine("cache", "github", "artifacts");
                string cachePath = _cachingService.GetCacheFilePath(cacheDir, cacheKey);

                var workflowArtifacts = await _cachingService.GetOrCreateAsync(
                    cachePath,
                    async () =>
                    {
                        var result = await _apiClient.GetArtifactsForWorkflowRunAsync(
                            repo.RepoOwner,
                            repo.RepoName,
                            runId,
                            cancellationToken);
                        return result?.ToList() ?? new List<GitHubArtifact>();
                    },
                    TimeSpan.FromMinutes(10)
                );

                if (workflowArtifacts != null && workflowArtifacts.Any())
                {
                    // Get workflow run details to populate metadata
                    var workflowRun = await GetWorkflowRunAsync(runId, cancellationToken);

                    if (workflowRun != null)
                    {
                        foreach (var artifact in workflowArtifacts)
                        {
                            artifact.RunId = workflowRun.RunId;
                            artifact.WorkflowId = workflowRun.WorkflowId;
                            artifact.WorkflowNumber = workflowRun.WorkflowNumber;
                            artifact.PullRequestNumber = workflowRun.PullRequestNumber;
                            artifact.PullRequestTitle = workflowRun.PullRequestTitle;
                            artifact.CommitSha = workflowRun.CommitSha;
                            artifact.CommitMessage = workflowRun.CommitMessage;
                            artifact.RepositoryInfo = repo;
                        }
                    }
                }

                return workflowArtifacts ?? Enumerable.Empty<GitHubArtifact>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting artifacts for workflow run {RunId}", runId);
                throw new GitHubServiceException($"Error getting artifacts for workflow run {runId}: {ex.Message}", ex);
            }
        }

        /// <inheritdoc />
        public async Task<IEnumerable<GitHubArtifact>> GetArtifactsForRepoAsync(long repoId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Getting artifacts for repository {RepoId}", repoId);

                // Get current repository information
                var repo = await _repoService.GetCurrentRepositoryAsync();

                // Get recent workflow runs for the repository
                var workflows = await GetWorkflowRunsForRepositoryAsync(repo, 1, 50, cancellationToken);

                // Collect artifacts from each workflow run
                var artifacts = new List<GitHubArtifact>();
                foreach (var workflow in workflows)
                {
                    try
                    {
                        var workflowArtifacts = await GetArtifactsForWorkflowRunAsync(workflow.RunId, cancellationToken);
                        artifacts.AddRange(workflowArtifacts);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error getting artifacts for workflow {WorkflowId}", workflow.RunId);
                    }
                }

                return artifacts;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting artifacts for repository {RepoId}", repoId);
                throw new GitHubServiceException($"Error getting artifacts for repository {repoId}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Gets all workflows from the repository
        /// </summary>
        public async Task<IEnumerable<GitHubWorkflow>> GetWorkflowsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Getting all workflows");

                var repo = await _repoService.GetCurrentRepositoryAsync();
                return await GetWorkflowRunsForRepositoryAsync(repo, cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all workflows");
                throw;
            }
        }

        /// <summary>
        /// Gets workflow runs for a specific workflow file
        /// </summary>
        public async Task<IEnumerable<GitHubWorkflow>> GetWorkflowRunsForWorkflowFileAsync(
            GitHubRepository repo,
            string workflowPath,
            int page = 1,
            int perPage = 30,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Getting workflow runs for workflow file: {WorkflowPath}", workflowPath);

                // Get the workflow ID from the workflow file
                var workflowId = await GetWorkflowIdAsync(repo, workflowPath, cancellationToken);

                if (string.IsNullOrEmpty(workflowId))
                {
                    _logger.LogWarning("Workflow file not found: {WorkflowPath}. Returning empty list.", workflowPath);
                    return new List<GitHubWorkflow>();
                }

                // Get workflow runs using the workflow ID - IMPORTANT: Use "actions/workflows/{workflowId}/runs" endpoint
                // which GitHubApiClient will expand to "repos/{owner}/{repo}/actions/workflows/{workflowId}/runs"
                string endpoint = $"actions/workflows/{workflowId}/runs?page={page}&per_page={perPage}";

                // Use caching for workflow runs
                string cacheKey = $"workflow_runs_{repo.RepoOwner}_{repo.RepoName}_{workflowId}_p{page}_pp{perPage}";
                
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string cacheDir = Path.Combine(appDataPath,"cache", "github", "workflows");
                Directory.CreateDirectory(cacheDir);
                string cachePath = _cachingService.GetCacheFilePath(cacheDir, cacheKey);

                var runs = await _cachingService.GetOrCreateAsync(
                    cachePath,
                    async () =>
                    {
                        // Pass the repository settings to GetAsync so it can construct the correct URL
                        var response = await _apiClient.GetAsync<HttpResponseMessage>(repo, endpoint, cancellationToken);
                        var json = await response.Content.ReadAsStringAsync(cancellationToken);
                        return ParseWorkflowRuns(json, repo).ToList();
                    },
                    TimeSpan.FromMinutes(5)
                );

                return runs ?? Enumerable.Empty<GitHubWorkflow>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting workflow runs for file {WorkflowPath}", workflowPath);
                throw new GitHubServiceException($"Error getting workflow runs for file {workflowPath}: {ex.Message}", ex);
            }
        }

        /// <inheritdoc />
        public async Task<IEnumerable<GitHubWorkflow>> GetWorkflowRunsForRepositoryAsync(
            GitHubRepository repo,
            int page = 1,
            int perPage = 30,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Getting workflow runs for repository: {Owner}/{Repo}",
                    repo.RepoOwner, repo.RepoName);

                // Use a simple caching mechanism
                string cacheKey = $"repo_workflow_runs_{repo.RepoOwner}_{repo.RepoName}_p{page}_pp{perPage}";
                
                // First check memory cache - use TryGetValue which is thread-safe
                List<GitHubWorkflow>? cachedRuns;
                bool hasCachedValue = false;
                
                lock (_workflowRunsCache) // Use lock instead of semaphore for simple access
                {
                    hasCachedValue = _workflowRunsCache.TryGetValue(cacheKey, out cachedRuns);
                }
                
                if (hasCachedValue && cachedRuns != null)
                {
                    _logger.LogDebug("Using in-memory cache for workflow runs");
                    return cachedRuns;
                }

                // Then check disk cache
                string cacheDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "GenHub", "cache", "github", "workflows");
                
                // Ensure cache directory exists
                Directory.CreateDirectory(cacheDir);
                
                string cachePath = Path.Combine(cacheDir, $"{cacheKey.Replace("/", "_").Replace(":", "_")}.json");

                // For the first page or small perPage values, use a shorter cache duration
                TimeSpan cacheDuration = (page == 1 && perPage <= 30) 
                    ? TimeSpan.FromMinutes(2)  // Short cache for frequently accessed data
                    : TimeSpan.FromMinutes(10); // Longer cache for less frequently accessed data

                // Set up a timeout token for the fetch operation
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken.CanBeCanceled ? cancellationToken : CancellationToken.None,
                    timeoutCts.Token);

                // Use a simplified approach to fetch or get from cache
                List<GitHubWorkflow> runs;
                
                // Check if cache file exists and is not expired
                if (File.Exists(cachePath) && 
                    (DateTime.Now - new FileInfo(cachePath).LastWriteTime) < cacheDuration)
                {
                    // Read from cache file
                    try
                    {
                        string json = await File.ReadAllTextAsync(cachePath, linkedCts.Token);
                        runs = JsonSerializer.Deserialize<List<GitHubWorkflow>>(json) ?? new List<GitHubWorkflow>();
                        _logger.LogDebug("Loaded workflow runs from disk cache");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error reading cache file, fetching fresh data");
                        runs = await FetchWorkflowRunsAsync(repo, page, perPage, linkedCts.Token);
                    }
                }
                else
                {
                    // Cache miss or expired, fetch fresh data
                    runs = await FetchWorkflowRunsAsync(repo, page, perPage, linkedCts.Token);
                    
                    // Save to disk cache
                    try 
                    {
                        string json = JsonSerializer.Serialize(runs);
                        await File.WriteAllTextAsync(cachePath, json, linkedCts.Token);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error saving workflow runs to cache file");
                    }
                }
                
                // Update memory cache
                lock (_workflowRunsCache)
                {
                    _workflowRunsCache[cacheKey] = runs;
                }
                
                return runs;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Operation was cancelled while getting workflow runs");
                return Enumerable.Empty<GitHubWorkflow>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting workflow runs for repository");
                return Enumerable.Empty<GitHubWorkflow>();
            }
        }
        
        /// <summary>
        /// Fetches workflow runs from the GitHub API
        /// </summary>
        private async Task<List<GitHubWorkflow>> FetchWorkflowRunsAsync(
            GitHubRepository repo, 
            int page, 
            int perPage,
            CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogDebug("Fetching workflow runs from GitHub API for {Owner}/{Repo}", 
                    repo.RepoOwner, repo.RepoName);
                
                // IMPORTANT: actions/runs will be correctly expanded by ApiClient to repos/{owner}/{repo}/actions/runs 
                string endpoint = $"actions/runs?page={page}&per_page={perPage}";
                    
                var response = await _apiClient.GetAsync<HttpResponseMessage>(repo, endpoint, cancellationToken);
                
                if (response == null || !response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to fetch workflows: {StatusCode}", 
                        response?.StatusCode.ToString() ?? "No response");
                    return new List<GitHubWorkflow>();
                }
                    
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                
                // Validate JSON content
                if (string.IsNullOrWhiteSpace(json))
                {
                    _logger.LogWarning("Empty JSON response received from GitHub API");
                    return new List<GitHubWorkflow>();
                }

                // Log the first 100 chars for debugging
                _logger.LogDebug("Response content preview: {Preview}", 
                    json.Length > 100 ? json.Substring(0, 100) + "..." : json);
                
                var result = ParseWorkflowRuns(json, repo).ToList();
                
                _logger.LogDebug("Successfully fetched {Count} workflow runs", result.Count);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching workflow runs from API");
                return new List<GitHubWorkflow>();  // Return empty list instead of throwing
            }
        }

        /// <summary>
        /// Gets artifacts for all workflows
        /// </summary>
        public async Task<IEnumerable<GitHubArtifact>> GetAvailableArtifactsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Getting available artifacts (limited set)");

                // Get current repository
                var repo = await _repoService.GetCurrentRepositoryAsync();

                // Get only the most recent workflow runs - limit to 10 initially
                var workflows = await GetWorkflowRunsForRepositoryAsync(repo, 1, 10, cancellationToken);

                // Get artifacts for each workflow - limited set
                var artifacts = new List<GitHubArtifact>();
                int count = 0;
                
                foreach (var workflow in workflows)
                {
                    try
                    {
                        // Limit to first 5 workflows to avoid excessive loading
                        if (count++ >= 5) break;
                        
                        // Use caching for artifacts per workflow run
                        string cacheKey = $"artifacts_{repo.RepoOwner}_{repo.RepoName}_{workflow.RunId}";
                        string cacheDir = Path.Combine("cache", "github", "artifacts");
                        
                        // Set a timeout for the fetch operation
                        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                            cancellationToken.CanBeCanceled ? cancellationToken : CancellationToken.None,
                            timeoutCts.Token);

                        var workflowArtifacts = await _cachingService.GetOrFetchAsync<List<GitHubArtifact>>(
                            cacheKey,
                            async () =>
                            {
                                var result = await _apiClient.GetArtifactsForWorkflowRunAsync(
                                    repo.RepoOwner,
                                    repo.RepoName,
                                    workflow.RunId,
                                    linkedCts.Token);
                                return result?.ToList() ?? new List<GitHubArtifact>();
                            },
                            TimeSpan.FromMinutes(10),
                            linkedCts.Token
                        );

                        // Process the artifacts
                        if (workflowArtifacts != null)
                        {
                            foreach (var artifact in workflowArtifacts)
                            {
                                // Add workflow metadata
                                artifact.RunId = workflow.RunId;
                                artifact.WorkflowId = workflow.WorkflowId;
                                artifact.WorkflowNumber = workflow.WorkflowNumber;
                                artifact.PullRequestNumber = workflow.PullRequestNumber;
                                artifact.PullRequestTitle = workflow.PullRequestTitle;
                                artifact.CommitSha = workflow.CommitSha;
                                artifact.CommitMessage = workflow.CommitMessage;
                                artifact.RepositoryInfo = repo;

                                artifacts.Add(artifact);
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogWarning("Operation cancelled while getting artifacts for workflow {RunId}", workflow.RunId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error getting artifacts for workflow {WorkflowId}", workflow.RunId);
                    }
                }

                return artifacts;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available artifacts");
                return Enumerable.Empty<GitHubArtifact>();
            }
        }

        /// <summary>
        /// Gets a specific workflow run by ID
        /// </summary>
        public async Task<GitHubWorkflow?> GetWorkflowRunAsync(long runId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Getting workflow run {RunId}", runId);

                // Get current repository
                var repo = await _repoService.GetCurrentRepositoryAsync();

                // Use caching for workflow run
                string cacheKey = $"workflow_run_{repo.RepoOwner}_{repo.RepoName}_{runId}";
                string cacheDir = Path.Combine("cache", "github", "workflows");
                string cachePath = _cachingService.GetCacheFilePath(cacheDir, cacheKey);

                var workflow = await _cachingService.GetOrCreateAsync(
                    cachePath,
                    async () =>
                    {
                        // IMPORTANT: Use actions/runs/{runId} which will be correctly expanded 
                        // to repos/{owner}/{repo}/actions/runs/{runId}
                        return await _apiClient.GetWorkflowRunAsync(
                            repo,
                            runId,
                            cancellationToken);
                    },
                    TimeSpan.FromMinutes(10)
                );

                return workflow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting workflow run {RunId}", runId);
                throw;
            }
        }

        /// <summary>
        /// Gets artifact counts for a list of workflow run IDs
        /// </summary>
        public async Task<IDictionary<long, int>> GetArtifactCountsForWorkflowsAsync(
            IEnumerable<long> runIds, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Getting artifact counts for {Count} workflows", runIds.Count());
                
                // Create a dictionary to store results
                var result = new Dictionary<long, int>();
                
                // We can optimize by checking cache first
                foreach (var runId in runIds)
                {
                    try
                    {
                        // First check if artifacts are already cached
                        var cachedArtifacts = await _cachingService.GetAsync<List<GitHubArtifact>>(
                            $"artifacts_{runId}", cancellationToken);
                        
                        if (cachedArtifacts != null)
                        {
                            result[runId] = cachedArtifacts.Count;
                            continue;
                        }
                        
                        // No cache hit, so we'll need to fetch artifact data
                        var repo = await _repoService.GetCurrentRepositoryAsync();
                        
                        // IMPORTANT: Use actions/runs/{runId}/artifacts endpoint which will be correctly expanded
                        var endpoint = $"actions/runs/{runId}/artifacts";
                        
                        var response = await _apiClient.GetAsync<HttpResponseMessage>(repo, endpoint, cancellationToken);
                        
                        if (response.IsSuccessStatusCode)
                        {
                            var json = await response.Content.ReadAsStringAsync(cancellationToken);
                            using var document = JsonDocument.Parse(json);
                            
                            // Get total count from the response
                            if (document.RootElement.TryGetProperty("total_count", out var totalCount))
                            {
                                result[runId] = totalCount.GetInt32();
                            }
                            else
                            {
                                result[runId] = 0;
                            }
                        }
                        else
                        {
                            result[runId] = 0;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to get artifact count for workflow {RunId}", runId);
                        result[runId] = 0; // Default to zero on error
                    }
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting artifact counts for workflows");
                throw new GitHubServiceException("Failed to get artifact counts", ex);
            }
        }

        /// <summary>
        /// Parse artifacts from an HTTP response
        /// </summary>
        public async Task<IEnumerable<GitHubArtifact>> ParseArtifactsResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken = default)
        {
            try
            {
                if (response == null) return Enumerable.Empty<GitHubArtifact>();
                
                // Check for 404 specifically - this means the run has no artifacts (which is normal)
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    _logger.LogInformation("No artifacts found (404 response)");
                    return Enumerable.Empty<GitHubArtifact>();
                }
                
                // For any other error, ensure success status code
                response.EnsureSuccessStatusCode();
                
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                
                // Parse the JSON response
                using var document = JsonDocument.Parse(content);
                var artifactsArray = document.RootElement.GetProperty("artifacts");
                
                // Check if there are no artifacts
                if (artifactsArray.ValueKind == JsonValueKind.Null || artifactsArray.GetArrayLength() == 0)
                {
                    _logger.LogInformation("No artifacts found in response");
                    return Enumerable.Empty<GitHubArtifact>();
                }
                
                var artifacts = new List<GitHubArtifact>();
                
                // Process each artifact
                foreach (var artifact in artifactsArray.EnumerateArray())
                {
                    if (artifact.ValueKind == JsonValueKind.Object)
                    {
                        var parsedArtifact = ParseArtifact(artifact);
                        artifacts.Add(parsedArtifact);
                    }
                }
                
                _logger.LogInformation("Parsed {Count} artifacts from response", artifacts.Count);
                return artifacts;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing artifacts response");
                return Enumerable.Empty<GitHubArtifact>();
            }
        }
        
        /// <summary>
        /// Parse an artifact from a JSON element
        /// </summary>
        private GitHubArtifact ParseArtifact(JsonElement artifactElement)
        {
            var artifact = new GitHubArtifact
            {
                Id = artifactElement.GetProperty("id").GetInt64(),
                Name = artifactElement.GetProperty("name").GetString() ?? string.Empty,
                SizeInBytes = artifactElement.GetProperty("size_in_bytes").GetInt64()
            };
            
            // Try to parse creation date
            if (artifactElement.TryGetProperty("created_at", out var createdAtProp) && 
                createdAtProp.ValueKind != JsonValueKind.Null)
            {
                if (DateTime.TryParse(createdAtProp.GetString(), out var createdAt))
                {
                    artifact.CreatedAt = createdAt;
                }
            }
            
            return artifact;
        }

        /// <summary>
        /// Gets the workflow ID for a workflow file
        /// </summary>
        private async Task<string> GetWorkflowIdAsync(
            GitHubRepository repoConfig,
            string workflowFile,
            CancellationToken cancellationToken)
        {
            // Use caching for workflow list
            string cacheKey = $"workflow_list_{repoConfig.RepoOwner}_{repoConfig.RepoName}";
            string cacheDir = Path.Combine("cache", "github", "workflows");
            string cachePath = _cachingService.GetCacheFilePath(cacheDir, cacheKey);

            string json = await _cachingService.GetOrCreateAsync(
                cachePath,
                async () =>
                {
                    // IMPORTANT: Use actions/workflows endpoint which will be expanded to repos/{owner}/{repo}/actions/workflows
                    string endpoint = "actions/workflows";
                    var response = await _apiClient.GetAsync<HttpResponseMessage>(repoConfig, endpoint, cancellationToken);
                    return await response.Content.ReadAsStringAsync(cancellationToken);
                },
                TimeSpan.FromMinutes(30)
            ) ?? string.Empty;

            using var document = JsonDocument.Parse(json);
            if (document.RootElement.TryGetProperty("workflows", out var workflows))
            {
                string fileName = Path.GetFileName(workflowFile);

                foreach (var workflow in workflows.EnumerateArray())
                {
                    if (workflow.TryGetProperty("path", out var path) && workflow.TryGetProperty("id", out var id))
                    {
                        string workflowPath = path.GetString() ?? string.Empty;

                        if (workflowPath.Equals(workflowFile, StringComparison.OrdinalIgnoreCase) ||
                            Path.GetFileName(workflowPath).Equals(fileName, StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.LogDebug("Found workflow ID {Id} for file {File}", id.ToString(), workflowFile);
                            return id.GetInt64().ToString();
                        }
                    }
                }
            }

            _logger.LogWarning("No workflow found for file: {WorkflowFile}", workflowFile);
            return string.Empty;
        }

        /// <summary>
        /// Parses workflow runs from JSON response
        /// </summary>
        private IEnumerable<GitHubWorkflow> ParseWorkflowRuns(string json, GitHubRepository repoConfig)
        {
            var result = new List<GitHubWorkflow>();

            try
            {
                // Check for empty JSON to avoid parsing errors
                if (string.IsNullOrWhiteSpace(json))
                {
                    _logger.LogWarning("Empty JSON response received when parsing workflows");
                    return result;
                }

                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;

                if (root.TryGetProperty("workflow_runs", out var workflowRunsElement))
                {
                    foreach (var run in workflowRunsElement.EnumerateArray())
                    {
                        try
                        {
                            var workflow = new GitHubWorkflow
                            {
                                // Create a new repository instance for each workflow
                                RepositoryInfo = new GitHubRepository
                                {
                                    RepoOwner = repoConfig.RepoOwner,
                                    RepoName = repoConfig.RepoName,
                                    DisplayName = repoConfig.DisplayName,
                                    Token = repoConfig.Token,
                                    WorkflowFile = repoConfig.WorkflowFile,
                                    Branch = repoConfig.Branch
                                }
                            };

                            // Parse basic workflow properties
                            if (run.TryGetProperty("id", out var id))
                                workflow.RunId = id.GetInt64();

                            if (run.TryGetProperty("workflow_id", out var workflowId))
                                workflow.WorkflowId = workflowId.GetInt64();

                            if (run.TryGetProperty("run_number", out var runNumber))
                                workflow.WorkflowNumber = runNumber.GetInt32();

                            // Enhanced workflow name parsing with multiple fallbacks
                            if (run.TryGetProperty("name", out var name) && !name.ValueEquals("null"))
                            {
                                workflow.Name = name.GetString() ?? "Unknown Workflow";
                                _logger.LogDebug("Found workflow name from 'name' field: {Name}", workflow.Name);
                            }
                            else if (run.TryGetProperty("display_title", out var displayTitle) && !displayTitle.ValueEquals("null"))
                            {
                                workflow.Name = displayTitle.GetString() ?? "Unknown Workflow";
                                _logger.LogDebug("Found workflow name from 'display_title' field: {Name}", workflow.Name);
                            }
                            else if (run.TryGetProperty("head_commit", out var headCommit) && 
                                     headCommit.TryGetProperty("message", out var commitMessage))
                            {
                                // Use first line of commit message as fallback
                                var commitMsg = commitMessage.GetString();
                                if (!string.IsNullOrEmpty(commitMsg))
                                {
                                    var firstLine = commitMsg.Split('\n')[0].Trim();
                                    workflow.Name = firstLine.Length > 50 ? firstLine.Substring(0, 47) + "..." : firstLine;
                                    _logger.LogDebug("Using commit message as workflow name: {Name}", workflow.Name);
                                }
                                else
                                {
                                    workflow.Name = "Unknown Workflow";
                                }
                            }
                            else
                            {
                                workflow.Name = "Unknown Workflow";
                                _logger.LogDebug("No workflow name found, using default: {Name}", workflow.Name);
                            }

                            // FIX: Enhanced workflow path parsing
                            if (run.TryGetProperty("path", out var path))
                            {
                                workflow.WorkflowPath = path.GetString();
                                
                                // If no name was found, extract from path as last resort
                                if (workflow.Name == "Unknown Workflow" && !string.IsNullOrEmpty(workflow.WorkflowPath))
                                {
                                    var fileName = System.IO.Path.GetFileNameWithoutExtension(workflow.WorkflowPath);
                                    workflow.Name = fileName.Replace("_", " ").Replace("-", " ");
                                    _logger.LogDebug("Extracted workflow name from path: {Name}", workflow.Name);
                                }
                            }

                            // Parse additional workflow properties
                            if (run.TryGetProperty("created_at", out var createdAt))
                            {
                                if (DateTime.TryParse(createdAt.GetString(), out var parsedCreatedAt))
                                    workflow.CreatedAt = parsedCreatedAt;
                            }

                            if (run.TryGetProperty("head_sha", out var headSha))
                                workflow.CommitSha = headSha.GetString() ?? string.Empty;

                            if (run.TryGetProperty("event", out var eventType))
                                workflow.EventType = eventType.GetString();

                            if (run.TryGetProperty("html_url", out var htmlUrl))
                            {
                                var url = htmlUrl.GetString() ?? string.Empty;
                                if (url.Contains("/actions/runs/"))
                                {
                                    workflow.HtmlUrl = url;
                                }
                            }

                            if (run.TryGetProperty("head_commit", out var headCommitElement) &&
                                headCommitElement.TryGetProperty("message", out var message))
                            {
                                workflow.CommitMessage = message.GetString();
                            }

                            if (run.TryGetProperty("pull_requests", out var pullRequests) &&
                                pullRequests.GetArrayLength() > 0)
                            {
                                var firstPr = pullRequests[0];
                                
                                if (firstPr.TryGetProperty("number", out var prNumber))
                                    workflow.PullRequestNumber = prNumber.GetInt32();

                                if (firstPr.TryGetProperty("title", out var prTitle))
                                    workflow.PullRequestTitle = prTitle.GetString();
                            }

                            // Log the parsed workflow for debugging
                            _logger.LogDebug("Parsed workflow: Name='{Name}', ID={WorkflowId}, RunNumber={RunNumber}, Path='{Path}'", 
                                workflow.Name, workflow.WorkflowId, workflow.WorkflowNumber, workflow.WorkflowPath);

                            result.Add(workflow);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error parsing individual workflow run");
                        }
                    }
                }
                else
                {
                    // Log the JSON content for better diagnostics
                    var preview = json.Length > 200 ? json.Substring(0, 200) + "..." : json;
                    _logger.LogWarning("No workflow_runs property found in response: {Preview}", preview);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing workflow runs JSON");
            }

            _logger.LogInformation("Parsed {Count} workflows from JSON response", result.Count);
            return result;
        }

        /// <inheritdoc />
        public async Task<GitHubWorkflow?> GetWorkflowRunAsync(
            GitHubRepository repoSettings, 
            long runId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("GetWorkflowRunAsync called for runId {RunId} in repo {Owner}/{Repo}", 
                    runId, repoSettings.RepoOwner, repoSettings.RepoName);

                // Use caching for workflow run
                string cacheKey = $"workflow_run_{repoSettings.RepoOwner}_{repoSettings.RepoName}_{runId}";
                string cacheDir = Path.Combine("cache", "github", "workflows");
                string cachePath = _cachingService.GetCacheFilePath(cacheDir, cacheKey);

                var workflow = await _cachingService.GetOrCreateAsync(
                    cachePath,
                    async () =>
                    {
                        _logger.LogDebug("Cache miss, fetching from API");
                        
                        // Use actions/runs/{runId} which will be expanded correctly
                        var result = await _apiClient.GetWorkflowRunAsync(
                            repoSettings,
                            runId,
                            cancellationToken);
                            
                        if (result != null)
                        {
                            _logger.LogDebug("API returned workflow: RunId={RunId}, WorkflowId={WorkflowId}, Name='{Name}'", 
                                result.RunId, result.WorkflowId, result.Name);
                        }
                        else
                        {
                            _logger.LogDebug("API returned null workflow");
                        }
                        
                        return result;
                    },
                    TimeSpan.FromMinutes(10)
                );

                // Ensure repository info is set
                if (workflow != null && workflow.RepositoryInfo == null)
                {
                    workflow.RepositoryInfo = new GitHubRepository
                    {
                        RepoOwner = repoSettings.RepoOwner,
                        RepoName = repoSettings.RepoName,
                        DisplayName = repoSettings.DisplayName,
                        Token = repoSettings.Token,
                        WorkflowFile = repoSettings.WorkflowFile,
                        Branch = repoSettings.Branch
                    };
                    
                    _logger.LogDebug("Set repository info on workflow");
                }

                return workflow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting workflow run {RunId} for repo {Owner}/{Repo}", 
                    runId, repoSettings.RepoOwner, repoSettings.RepoName);
                return null;
            }
        }
    }
}
