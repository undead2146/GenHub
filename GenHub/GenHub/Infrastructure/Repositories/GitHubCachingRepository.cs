using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Text.Json;
using System.Threading.Tasks;

using GenHub.Core.Interfaces.Repositories;
using GenHub.Core.Models;
using GenHub.Core.Interfaces.Caching;

using Microsoft.Extensions.Logging;

namespace GenHub.Infrastructure.Repositories
{
    /// <summary>
    /// Repository for GitHub data caching
    /// </summary>
    public class GitHubCachingRepository : IGitHubCachingRepository
    {
        private readonly ILogger<GitHubCachingRepository> _logger;
        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly string _baseCachePath;
        private readonly Dictionary<string, DateTime> _cacheTimestamps = new();
        private readonly ICacheService _cacheService;
        
        // Constants for shared settings
        private const string REPOSITORIES_SETTING = "github_repositories";
        private const string CURRENT_REPOSITORY_SETTING = "current_github_repository";

        /// <summary>
        /// Creates a new instance of GitHubCachingRepository
        /// </summary>
        public GitHubCachingRepository(ILogger<GitHubCachingRepository> logger, ICacheService cacheService)
        {
            _logger = logger;
            _cacheService = cacheService;

            // Set up the cache directory in user's app data
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _baseCachePath = Path.Combine(appDataPath, "GenHub", "Cache");

            // Ensure directory exists
            if (!Directory.Exists(_baseCachePath))
            {
                Directory.CreateDirectory(_baseCachePath);
            }

            _logger.LogInformation("GitHub data repository initialized with cache path: {CachePath}", _baseCachePath);
        }

        /// <summary>
        /// Gets the repository cache path
        /// </summary>
        private string GetRepoCachePath(string owner, string name)
        {
            // Fixed path structure: always ensure owner/name format
            string repoCachePath = Path.Combine(_baseCachePath, owner, name);

            if (!Directory.Exists(repoCachePath))
            {
                Directory.CreateDirectory(repoCachePath);
            }

            _logger.LogDebug("Using repo cache path: {Path} for {Owner}/{Name}", repoCachePath, owner, name);
            return repoCachePath;
        }
        
        /// <summary>
        /// Saves a collection of workflows to the cache for a specific repository
        /// </summary>
        /// <param name="workflows">The list of GitHubWorkflow objects to save</param>
        /// <param name="repositoryId">The unique identifier of the repository (e.g., "owner/repoName").</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public async Task SaveWorkflowsAsync(IEnumerable<GitHubWorkflow> workflows, string repositoryId, CancellationToken cancellationToken = default)
        {
            try
            {
                // Split repository name to get owner and name
                var parts = repositoryId.Split('/');
                if (parts.Length != 2)
                {
                    _logger.LogWarning("Invalid repository full name format: {RepoName}, expected format: owner/name", repositoryId);
                    return;
                }

                string owner = parts[0];
                string name = parts[1];

                // Create the cache directory structure: owner/repo/
                var repoCachePath = GetRepoCachePath(owner, name);
                var cacheFilePath = Path.Combine(repoCachePath, "workflows.json");

                // Ensure each workflow has repository info
                var workflowList = workflows.ToList();
                foreach (var workflow in workflowList)
                {
                    if (workflow.RepositoryInfo == null)
                    {
                        workflow.RepositoryInfo = new GitHubRepoSettings
                        {
                            RepoOwner = owner,
                            RepoName = name,
                            DisplayName = $"{owner}/{name}"
                        };
                    }

                    // If this workflow has artifacts, cache them separately
                    if (workflow.Artifacts != null && workflow.Artifacts.Any())
                    {
                        try
                        {
                            // Cache artifacts for this workflow
                            await CacheArtifactsAsync(repositoryId, workflow.RunId, workflow.Artifacts);

                            _logger.LogDebug("Cached {Count} artifacts for workflow {WorkflowId}", 
                                workflow.Artifacts.Count, workflow.RunId);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error caching artifacts for workflow {WorkflowId}", workflow.RunId);
                        }
                    }
                }

                // Use CacheService to save to cache
                await _cacheService.SaveToCacheAsync(cacheFilePath, workflowList, null, cancellationToken);

                // Update cache timestamp
                _cacheTimestamps[$"workflows_{repositoryId}"] = DateTime.UtcNow;

                _logger.LogInformation("Saved {Count} workflows for repository {RepoName}", workflowList.Count, repositoryId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving workflows for repository {RepoName}", repositoryId);
            }
        }

        /// <summary>
        /// Gets cached workflow data for a repository
        /// </summary>
        public async Task<IEnumerable<GitHubWorkflow>> GetCachedWorkflowsAsync(string repoFullName, CancellationToken cancellationToken = default)
        {
            try
            {
                // Split repository name to get owner and name
                var parts = repoFullName.Split('/');
                if (parts.Length != 2)
                {
                    _logger.LogWarning("Invalid repository full name format: {RepoName}, expected format: owner/name", repoFullName);
                    return null;
                }

                string owner = parts[0];
                string name = parts[1];

                // Create the cache directory structure: owner/repo/
                var repoCachePath = GetRepoCachePath(owner, name);
                var cacheFilePath = Path.Combine(repoCachePath, "workflows.json");

                _logger.LogDebug("Looking for cached workflows at: {CachePath}", cacheFilePath);

                if (!File.Exists(cacheFilePath))
                {
                    _logger.LogInformation("No cached workflows found for repository {RepoName}", repoFullName);
                    return null;
                }

                // Check if cache is expired (more than 1 hour old) using CachingService
                if (await _cacheService.IsCacheExpiredAsync(cacheFilePath, cancellationToken))
                {
                    _logger.LogInformation("Cached workflows for repository {RepoName} are expired", repoFullName);
                    return null;
                }

                // Read and deserialize the cached data using CachingService
                var result = await _cacheService.GetFromCacheAsync<List<GitHubWorkflow>>(cacheFilePath, cancellationToken);
                return result ?? new List<GitHubWorkflow>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cached workflows for repository {RepoName}", repoFullName);
                return new List<GitHubWorkflow>();
            }
        }

        /// <summary>
        /// Loads cached artifacts for workflows
        /// </summary>
        private async Task LoadArtifactsForWorkflowsAsync(IEnumerable<GitHubWorkflow>? workflows, string repoCachePath, CancellationToken cancellationToken)
        {
            if (workflows == null) return;

            // For each workflow, check if we have cached artifacts
            foreach (var workflow in workflows)
            {
                try
                {
                    var artifactsCacheFile = Path.Combine(repoCachePath, $"artifacts_{workflow.RunId}.json");

                    if (File.Exists(artifactsCacheFile))
                    {
                        // Use CachingService to load artifacts
                        var artifacts = await _cacheService.GetFromCacheAsync<List<GitHubArtifact>>(artifactsCacheFile, cancellationToken);

                        if (artifacts != null && artifacts.Any())
                        {
                            // Ensure repository info is set for each artifact
                            foreach (var artifact in artifacts)
                            {
                                if (artifact.RepositoryInfo == null)
                                {
                                    artifact.RepositoryInfo = workflow.RepositoryInfo;
                                }

                                // Match the workflow run details to ensure we have complete data
                                artifact.RunId = workflow.RunId;
                                artifact.WorkflowId = workflow.WorkflowId;
                            }

                            _logger.LogDebug("Loaded {Count} cached artifacts for workflow {WorkflowId}",
                                artifacts.Count, workflow.RunId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error loading cached artifacts for workflow {WorkflowId}", workflow.RunId);
                }
            }
        }

        /// <summary>
        /// Caches workflow data for a repository
        /// </summary>
        public async Task CacheWorkflowsAsync(string repoFullName, IEnumerable<GitHubWorkflow> workflows, CancellationToken cancellationToken = default)
        {
            try
            {
                // Split repository name to get owner and name
                var parts = repoFullName.Split('/');
                if (parts.Length != 2)
                {
                    _logger.LogWarning("Invalid repository full name format: {RepoName}, expected format: owner/name", repoFullName);
                    return;
                }

                string owner = parts[0];
                string name = parts[1];

                // Create the cache directory structure: owner/repo/
                var repoCachePath = GetRepoCachePath(owner, name);
                var cacheFilePath = Path.Combine(repoCachePath, "workflows.json");

                // Ensure each workflow has repository info
                var workflowList = workflows.ToList();
                foreach (var workflow in workflowList)
                {
                    if (workflow.RepositoryInfo == null)
                    {
                        workflow.RepositoryInfo = new GitHubRepoSettings
                        {
                            RepoOwner = owner,
                            RepoName = name,
                            DisplayName = $"{owner}/{name}"
                        };
                    }

                    // If this workflow has artifacts, cache them separately
                    if (workflow.Artifacts != null && workflow.Artifacts.Any())
                    {
                        try
                        {
                            // Cache artifacts for this workflow
                            await CacheArtifactsAsync(repoFullName, workflow.RunId, workflow.Artifacts);

                            _logger.LogDebug("Cached {Count} artifacts for workflow {WorkflowId}",
                                workflow.Artifacts.Count, workflow.RunId);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error caching artifacts for workflow {WorkflowId}", workflow.RunId);
                        }
                    }
                }

                // Use CachingService to save to cache
                await _cacheService.SaveToCacheAsync(cacheFilePath, workflowList, null, cancellationToken);

                // Update cache timestamp
                _cacheTimestamps[$"workflows_{repoFullName}"] = DateTime.UtcNow;

                _logger.LogInformation("Cached {Count} workflows for repository {RepoName}", workflowList.Count, repoFullName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error caching workflows for repository {RepoName}", repoFullName);
            }
        }

        /// <summary>
        /// Caches artifacts for a workflow run
        /// </summary>
        public async Task CacheArtifactsAsync(string repoFullName, long runId, IEnumerable<GitHubArtifact> artifacts, CancellationToken cancellationToken = default)
        {
            try
            {
                // Split repository name to get owner and name
                var parts = repoFullName.Split('/');
                if (parts.Length != 2)
                {
                    _logger.LogWarning("Invalid repository full name format: {RepoName}, expected format: owner/name", repoFullName);
                    return;
                }

                string owner = parts[0];
                string name = parts[1];

                // Create the cache directory structure: owner/repo/
                var repoCachePath = GetRepoCachePath(owner, name);
                var cacheFilePath = Path.Combine(repoCachePath, $"artifacts_{runId}.json");

                // Ensure each artifact has repository info
                var artifactList = artifacts.ToList();
                foreach (var artifact in artifactList)
                {
                    if (artifact.RepositoryInfo == null)
                    {
                        artifact.RepositoryInfo = new GitHubRepoSettings
                        {
                            RepoOwner = owner,
                            RepoName = name,
                            DisplayName = $"{owner}/{name}"
                        };
                    }
                }

                // Use CachingService to save to cache
                await _cacheService.SaveToCacheAsync(cacheFilePath, artifactList, null, cancellationToken);

                _logger.LogInformation("Cached {Count} artifacts for workflow {WorkflowId}", artifactList.Count(), runId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error caching artifacts for workflow {WorkflowId}", runId);
            }
        }

        /// <summary>
        /// Gets cached artifacts for a workflow run
        /// </summary>
        public async Task<IEnumerable<GitHubArtifact>> GetCachedArtifactsAsync(string repoFullName, long runId, CancellationToken cancellationToken = default)
        {
            try
            {
                // Split repository name to get owner and name
                var parts = repoFullName.Split('/');
                if (parts.Length != 2)
                {
                    _logger.LogWarning("Invalid repository full name format: {RepoName}, expected format: owner/name", repoFullName);
                    return new List<GitHubArtifact>();
                }

                string owner = parts[0];
                string name = parts[1];

                // Create the cache directory structure: owner/repo/
                var repoCachePath = GetRepoCachePath(owner, name);
                var cacheFilePath = Path.Combine(repoCachePath, $"artifacts_{runId}.json");

                _logger.LogDebug("Looking for cached artifacts at: {CachePath}", cacheFilePath);

                if (!File.Exists(cacheFilePath))
                {
                    _logger.LogDebug("No cached artifacts found for workflow {RunId}", runId);
                    return new List<GitHubArtifact>();
                }

                // Check if cache is expired (more than 1 hour old)
                if (await _cacheService.IsCacheExpiredAsync(cacheFilePath, cancellationToken))
                {
                    _logger.LogDebug("Cached artifacts for workflow {RunId} are expired", runId);
                    return new List<GitHubArtifact>();
                }

                // Read and deserialize the cached data
                var artifacts = await _cacheService.GetFromCacheAsync<List<GitHubArtifact>>(cacheFilePath, cancellationToken);

                if (artifacts == null)
                {
                    return new List<GitHubArtifact>();
                }

                // Ensure each artifact has repository info
                foreach (var artifact in artifacts)
                {
                    if (artifact.RepositoryInfo == null)
                    {
                        artifact.RepositoryInfo = new GitHubRepoSettings
                        {
                            RepoOwner = owner,
                            RepoName = name,
                            DisplayName = $"{owner}/{name}"
                        };
                    }
                }

                _logger.LogInformation("Loaded {Count} cached artifacts for workflow {RunId}", artifacts.Count, runId);
                return artifacts;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cached artifacts for workflow {WorkflowId}", runId);
                return new List<GitHubArtifact>();
            }
        }

        /// <summary>
        /// Gets all configured repositories
        /// </summary>
        public async Task<IEnumerable<GitHubRepoSettings>> GetRepositoriesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Get repositories from shared settings
                var repositories = await _cacheService.GetSharedSettingAsync<List<GitHubRepoSettings>>(
                    REPOSITORIES_SETTING, 
                    cancellationToken);

                return repositories ?? new List<GitHubRepoSettings>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting repositories from shared settings");
                return new List<GitHubRepoSettings>();
            }
        }

        /// <summary>
        /// Saves the list of repositories
        /// </summary>
        public async Task SaveRepositoriesAsync(IEnumerable<GitHubRepoSettings> repositories, CancellationToken cancellationToken = default)
        {
            try
            {
                // Save repositories to shared settings
                await _cacheService.SaveSharedSettingAsync(
                    REPOSITORIES_SETTING, 
                    repositories.ToList(), 
                    cancellationToken);

                _logger.LogInformation("Saved {Count} repositories to shared settings", repositories.Count());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving repositories to shared settings");
            }
        }

        /// <summary>
        /// Gets the currently selected repository
        /// </summary>
        public async Task<GitHubRepoSettings?> GetCurrentRepositoryAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Get current repository from shared settings
                return await _cacheService.GetSharedSettingAsync<GitHubRepoSettings>(
                    CURRENT_REPOSITORY_SETTING, 
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current repository from shared settings");
                return null;
            }
        }

        /// <summary>
        /// Saves the currently selected repository
        /// </summary>
        public async Task SaveCurrentRepositoryAsync(GitHubRepoSettings repository, CancellationToken cancellationToken = default)
        {
            try
            {
                // Save current repository to shared settings
                await _cacheService.SaveSharedSettingAsync(
                    CURRENT_REPOSITORY_SETTING, 
                    repository, 
                    cancellationToken);

                _logger.LogInformation("Saved current repository to shared settings: {Owner}/{Repo}",
                    repository.RepoOwner, repository.RepoName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving current repository to shared settings");
            }
        }

        /// <summary>
        /// Determines if a cache is expired
        /// </summary>
        public bool IsCacheExpired(string key, TimeSpan duration)
        {
            if (!_cacheTimestamps.TryGetValue(key, out DateTime timestamp))
            {
                string filePath = GetFilePath(key);
                if (!File.Exists(filePath))
                {
                    return true;
                }

                // Get last write time of the cache file
                timestamp = File.GetLastWriteTimeUtc(filePath);
                _cacheTimestamps[key] = timestamp;
            }

            return DateTime.UtcNow - timestamp > duration;
        }

        /// <summary>
        /// Gets the file path for a cache key
        /// </summary>
        private string GetFilePath(string key)
        {
            return Path.Combine(_baseCachePath, key);
        }
    }
}
