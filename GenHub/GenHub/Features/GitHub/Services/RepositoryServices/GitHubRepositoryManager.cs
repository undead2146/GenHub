using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Interfaces.Caching;
using GenHub.Core.Interfaces.GitHub;
using GenHub.Core.Models.GitHub;
using GenHub.Core.Models;
using GenHub.Core.Models.Results;
using Microsoft.Extensions.Logging;

namespace GenHub.Features.GitHub.Services
{
    /// <summary>
    /// Manages the storage and retrieval of GitHub repositories, with an async-first approach.
    /// </summary>
    public class GitHubRepositoryManager : IGitHubRepositoryManager
    {
        private readonly ILogger<GitHubRepositoryManager> _logger;
        private readonly ICacheService _cacheService;
        private readonly string _repositoriesFilePath;
        private readonly string _currentRepositoryFilePath;
        private bool _initialized = false;

        private static readonly SemaphoreSlim _fileLock = new SemaphoreSlim(1, 1);
        private const string REPOSITORIES_KEY = "github_repositories";
        private const string CURRENT_REPOSITORY_KEY = "current_github_repository";        public GitHubRepositoryManager(
            ILogger<GitHubRepositoryManager> logger,
            ICacheService cacheService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));

            // Use cache service's shared settings directory for consistency
            var sharedSettingsDir = _cacheService.GetSharedSettingsDirectory();
            _repositoriesFilePath = Path.Combine(sharedSettingsDir, $"{REPOSITORIES_KEY}.json");
            _currentRepositoryFilePath = Path.Combine(sharedSettingsDir, $"{CURRENT_REPOSITORY_KEY}.json");

            _logger.LogDebug("GitHubRepositoryManager initialized. Using shared settings directory for consistent storage");
        }        private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
        {
            if (_initialized) return;

            await _fileLock.WaitAsync(cancellationToken);
            try
            {
                // Double-check after getting the lock
                if (_initialized) return;

                _logger.LogDebug("Initializing repository manager state...");

                // Check if we have repositories in cache service
                var cachedRepos = await _cacheService.GetSharedSettingAsync<List<GitHubRepository>>(REPOSITORIES_KEY, cancellationToken);
                if (cachedRepos == null || !cachedRepos.Any())
                {
                    _logger.LogInformation("No repositories found in cache. Initializing with defaults.");
                    var defaultRepos = GetDefaultRepositories();
                    await SaveRepositoriesInternalAsync(defaultRepos, cancellationToken);
                }
                
                _initialized = true;
                _logger.LogDebug("Repository manager initialization complete.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during repository manager initialization. Falling back to defaults.");
                var defaultRepos = GetDefaultRepositories();
                await SaveRepositoriesInternalAsync(defaultRepos, cancellationToken);
                _initialized = true; // Prevent re-initialization loops
            }
            finally
            {
                _fileLock.Release();
            }
        }public async Task<IEnumerable<GitHubRepository>> GetRepositoriesAsync(CancellationToken cancellationToken = default)
        {
            await EnsureInitializedAsync(cancellationToken);

            await _fileLock.WaitAsync(cancellationToken);
            try
            {
                // Try to get from cache service first
                var cachedRepos = await _cacheService.GetSharedSettingAsync<List<GitHubRepository>>(REPOSITORIES_KEY, cancellationToken);
                
                _logger.LogInformation("=== REPOSITORY MANAGER DEBUG ===");
                _logger.LogInformation($"Raw cached repositories count: {cachedRepos?.Count ?? 0}");
                
                if (cachedRepos != null && cachedRepos.Any())
                {
                    // Log details about each repository for debugging
                    _logger.LogInformation("Raw cached repositories breakdown:");
                    for (int i = 0; i < cachedRepos.Count; i++)
                    {
                        var repo = cachedRepos[i];
                        if (repo == null)
                        {
                            _logger.LogWarning($"   {i + 1}. NULL REPOSITORY ENTRY");
                            continue;
                        }
                        
                        var isValid = repo.IsValid;
                        _logger.LogInformation($"   {i + 1}. {repo.RepoOwner}/{repo.RepoName} - Valid: {isValid} - Enabled: {repo.Enabled}");
                        
                        if (!isValid)
                        {
                            _logger.LogWarning($"      Invalid because: Owner='{repo.RepoOwner}', Name='{repo.RepoName}'");
                        }
                    }
                    
                    var validRepositories = cachedRepos.Where(r => r != null && r.IsValid).ToList();
                    
                    _logger.LogInformation($"Valid repositories after filtering: {validRepositories.Count}");
                    _logger.LogInformation("Valid repositories list:");
                    for (int i = 0; i < validRepositories.Count; i++)
                    {
                        var repo = validRepositories[i];
                        _logger.LogInformation($"   {i + 1}. {repo.RepoOwner}/{repo.RepoName} - {repo.DisplayName}");
                    }
                    
                    if (validRepositories.Any())
                    {
                        return validRepositories;
                    }
                    else
                    {
                        _logger.LogWarning("No valid repositories found in cache, falling back to legacy or defaults");
                    }
                }
                else
                {
                    _logger.LogInformation("No cached repositories found, checking legacy storage");
                }

                // Fallback to legacy file system for migration scenarios
                var legacyPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GenHub", REPOSITORIES_KEY);
                if (File.Exists(legacyPath))
                {
                    _logger.LogInformation("Found legacy repositories file, migrating to cache service");
                    var json = await File.ReadAllTextAsync(legacyPath, cancellationToken);
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        var repositories = JsonSerializer.Deserialize<List<GitHubRepository>>(json) ?? new List<GitHubRepository>();
                        var validRepositories = repositories.Where(r => r != null && r.IsValid).ToList();

                        if (validRepositories.Any())
                        {
                            // Migrate to cache service
                            await _cacheService.SaveSharedSettingAsync(REPOSITORIES_KEY, validRepositories, cancellationToken);
                            _logger.LogInformation("Migrated {Count} repositories to cache service", validRepositories.Count);
                            
                            // Clean up legacy file
                            try
                            {
                                File.Delete(legacyPath);
                                _logger.LogInformation("Cleaned up legacy repositories file");
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Could not delete legacy repositories file");
                            }
                            
                            return validRepositories;
                        }
                    }
                }

                // Return defaults if nothing found
                return GetDefaultRepositories();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting repositories asynchronously. Returning defaults.");
                return GetDefaultRepositories();
            }
            finally
            {
                _fileLock.Release();
            }
        }

        public async Task<OperationResult> AddOrUpdateRepositoriesAsync(IEnumerable<GitHubRepository> repositoriesToAdd, bool replaceExisting = false, CancellationToken cancellationToken = default)
        {
            var validReposToAdd = repositoriesToAdd?.Where(r => r.IsValid).ToList();
            if (validReposToAdd == null || !validReposToAdd.Any())
            {
                return OperationResult.Succeeded("No valid repositories provided to add or update.");
            }

            await _fileLock.WaitAsync(cancellationToken);
            try
            {
                var existingReposList = (await GetRepositoriesAsync(cancellationToken)).ToList();
                var existingReposDict = existingReposList.ToDictionary(r => $"{r.RepoOwner}/{r.RepoName}".ToLowerInvariant());

                int addedCount = 0;
                int updatedCount = 0;

                foreach (var repoToAdd in validReposToAdd)
                {
                    var key = $"{repoToAdd.RepoOwner}/{repoToAdd.RepoName}".ToLowerInvariant();
                    if (existingReposDict.TryGetValue(key, out var existingRepo))
                    {
                        if (replaceExisting)
                        {
                            // Update metadata but preserve user-configurable state like 'Enabled'
                            existingRepo.DisplayName = repoToAdd.DisplayName;
                            existingRepo.Description = repoToAdd.Description;
                            existingRepo.Branch = repoToAdd.Branch;
                            existingRepo.PushedAt = repoToAdd.PushedAt;
                            existingRepo.StargazersCount = repoToAdd.StargazersCount;
                            existingRepo.ForksCount = repoToAdd.ForksCount;
                            updatedCount++;
                        }
                    }
                    else
                    {
                        existingReposList.Add(repoToAdd);
                        addedCount++;
                    }
                }

                if (addedCount > 0 || updatedCount > 0)
                {
                    await SaveRepositoriesInternalAsync(existingReposList, cancellationToken);
                    var message = $"Operation successful. Added: {addedCount}, Updated: {updatedCount}.";
                    _logger.LogInformation(message);
                    return OperationResult.Succeeded(message);
                }

                _logger.LogInformation("No new repositories were added or existing ones updated.");
                return OperationResult.Succeeded("No changes were made to the repository list.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add or update repositories.");
                return OperationResult.Failed($"An error occurred while saving repositories: {ex.Message}");
            }
            finally
            {
                _fileLock.Release();
            }
        }        private async Task SaveRepositoriesInternalAsync(IEnumerable<GitHubRepository> repositories, CancellationToken cancellationToken)
        {
            var validRepos = repositories.Where(r => r != null && r.IsValid).ToList();
            
            // Use only the cache service for consistent storage - no more duplication!
            await _cacheService.SaveSharedSettingAsync(REPOSITORIES_KEY, validRepos, cancellationToken);
            
            _logger.LogInformation("Saved {Count} repositories via cache service", validRepos.Count);
        }

        public IEnumerable<GitHubRepository> GetRepositories() => GetRepositoriesAsync().GetAwaiter().GetResult();

        public void SaveRepositories(IEnumerable<GitHubRepository> repositories) => SaveRepositoriesInternalAsync(repositories, CancellationToken.None).GetAwaiter().GetResult();

        public GitHubRepository GetDefaultRepository() => GetDefaultRepositories().First();

        private IEnumerable<GitHubRepository> GetDefaultRepositories()
        {
            return new List<GitHubRepository>
            {
                new GitHubRepository
                {
                    RepoOwner = "TheSuperHackers",
                    RepoName = "GeneralsGameCode",
                    DisplayName = "Generals Game Code (Community)",
                    Description = "Community-maintained codebase for C&C Generals",
                    Branch = "main",
                    Enabled = true,
                    LastAccessed = DateTime.UtcNow
                }
            };
        }
        
        // Other methods (GetCurrentRepository, SaveCurrentRepository, etc.) would also benefit from this async refactoring,
        // but are omitted here to focus on the primary change for repository list management.
        // The existing synchronous implementations for them will continue to function.

        public async Task<GitHubRepository> GetCurrentRepositoryAsync() => await Task.Run(() => GetCurrentRepository());
        public async Task SaveCurrentRepositoryAsync(GitHubRepository repository) => await Task.Run(() => SaveCurrentRepository(repository));
        public async Task<bool> ValidateRepositoryAsync(string owner, string repo, CancellationToken cancellationToken = default)
        {
            await Task.Delay(100, cancellationToken);
            return !string.IsNullOrWhiteSpace(owner) && !string.IsNullOrWhiteSpace(repo);
        }
        public async Task<bool> ValidateRepositoryAsync(GitHubRepository repository, CancellationToken cancellationToken = default)
        {
            if (repository == null || !repository.IsValid) return false;
            return await ValidateRepositoryAsync(repository.RepoOwner, repository.RepoName, cancellationToken);
        }
        public GitHubRepository GetCurrentRepository()
        {
            // This implementation can be refactored to be async as well.
            // For now, keeping it simple.
            if (File.Exists(_currentRepositoryFilePath))
            {
                try
                {
                    var json = File.ReadAllText(_currentRepositoryFilePath);
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        var repo = JsonSerializer.Deserialize<GitHubRepository>(json);
                        if (repo != null && repo.IsValid) return repo;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to read current repository file.");
                }
            }
            return GetRepositories().FirstOrDefault() ?? GetDefaultRepository();
        }
        public void SaveCurrentRepository(GitHubRepository repository)
        {
            if (repository == null || !repository.IsValid)
            {
                _logger.LogWarning("Attempted to save an invalid or null repository as current.");
                return;
            }
            var options = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var json = JsonSerializer.Serialize(repository, options);
            File.WriteAllText(_currentRepositoryFilePath, json);
        }
        
        /// <summary>
        /// Cleans up duplicate repository files from the old direct storage approach
        /// </summary>
        public async Task<bool> CleanupDuplicateRepositoryFilesAsync()
        {
            try
            {
                var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GenHub");
                var legacyRepoPath = Path.Combine(appDataPath, REPOSITORIES_KEY);
                var legacyCurrentPath = Path.Combine(appDataPath, CURRENT_REPOSITORY_KEY);
                
                bool cleanedAny = false;
                
                // Clean up legacy repositories file (without .json extension)
                if (File.Exists(legacyRepoPath))
                {
                    try
                    {
                        File.Delete(legacyRepoPath);
                        _logger.LogInformation("Cleaned up legacy repositories file: {Path}", legacyRepoPath);
                        cleanedAny = true;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not delete legacy repositories file: {Path}", legacyRepoPath);
                    }
                    
                    // Also clean up backup if it exists
                    var backupPath = legacyRepoPath + ".backup";
                    if (File.Exists(backupPath))
                    {
                        try
                        {
                            File.Delete(backupPath);
                            _logger.LogInformation("Cleaned up legacy repositories backup: {Path}", backupPath);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Could not delete legacy repositories backup: {Path}", backupPath);
                        }
                    }
                }
                
                // Clean up legacy current repository file
                if (File.Exists(legacyCurrentPath))
                {
                    try
                    {
                        File.Delete(legacyCurrentPath);
                        _logger.LogInformation("Cleaned up legacy current repository file: {Path}", legacyCurrentPath);
                        cleanedAny = true;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not delete legacy current repository file: {Path}", legacyCurrentPath);
                    }
                }
                
                if (cleanedAny)
                {
                    _logger.LogInformation("Duplicate repository file cleanup completed successfully");
                }
                else
                {
                    _logger.LogInformation("No duplicate repository files found to clean up");
                }
                
                return cleanedAny;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during duplicate repository file cleanup");
                return false;
            }
        }
    }
}
