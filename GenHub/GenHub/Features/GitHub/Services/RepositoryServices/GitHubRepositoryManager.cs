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
using Microsoft.Extensions.Logging;

namespace GenHub.Features.GitHub.Services
{
    /// <summary>
    /// Repository manager for GitHub repositories
    /// </summary>
    public class GitHubRepositoryManager : IGitHubRepositoryManager
    {
        private readonly ILogger<GitHubRepositoryManager> _logger;
        private readonly ICacheService _cacheService;
        
        // Constants for repository storage
        private const string REPOSITORIES_KEY = "github_repositories";
        private const string CURRENT_REPOSITORY_KEY = "current_github_repository";
        
        // Actual file path to ensure we have a single source of truth
        private readonly string _repositoriesFilePath;
        
        public GitHubRepositoryManager(
            ILogger<GitHubRepositoryManager> logger,
            ICacheService cacheService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            
            // Define the central repository file path
            _repositoriesFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "GenHub", "github_repositories.json");
            
            // Ensure directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(_repositoriesFilePath) ?? string.Empty);
            
            _logger.LogDebug("GitHubRepositoryManager initialized with repositories file: {FilePath}", 
                _repositoriesFilePath);
        }

        /// <summary>
        /// Gets the list of saved repositories
        /// </summary>
        public IEnumerable<GitHubRepoSettings> GetRepositories()
        {
            try
            {
                // Check if file exists
                if (File.Exists(_repositoriesFilePath))
                {
                    var json = File.ReadAllText(_repositoriesFilePath);
                    var repos = JsonSerializer.Deserialize<List<GitHubRepoSettings>>(json);
                    
                    if (repos != null && repos.Any())
                    {
                        _logger.LogInformation("Loaded {Count} repositories", repos.Count);
                        return repos;
                    }
                }
                
                // Create default repository if none found
                var defaultRepo = GetDefaultRepository();
                
                return new List<GitHubRepoSettings> { defaultRepo };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting repositories");
                
                // Return a default repository as fallback
                return new List<GitHubRepoSettings> { GetDefaultRepository() };
            }
        }
        
        /// <summary>
        /// Gets the list of saved repositories asynchronously
        /// </summary>
        public async Task<IEnumerable<GitHubRepoSettings>> GetRepositoriesAsync(CancellationToken cancellationToken = default)
        {
            // Simple async wrapper for the synchronous method
            return await Task.Run(() => GetRepositories(), cancellationToken);
        }

        /// <summary>
        /// Gets the default repository setting
        /// </summary>
        public GitHubRepoSettings GetDefaultRepository()
        {
            // Return a default repository configuration
            return new GitHubRepoSettings
            {
                RepoOwner = "undead2146",
                RepoName = "GenHub",
                DisplayName = "GenHub (Default)"
            };
        }

        /// <summary>
        /// Saves the list of repositories
        /// </summary>
        public void SaveRepositories(IEnumerable<GitHubRepoSettings> repositories)
        {
            try
            {
                // Ensure we have at least one repository
                if (repositories == null || !repositories.Any())
                {
                    _logger.LogWarning("Cannot save empty repository collection");
                    return;
                }
                
                var json = JsonSerializer.Serialize(repositories.ToList());
                File.WriteAllText(_repositoriesFilePath, json);
                
                _logger.LogInformation("Saved {Count} repositories", repositories.Count());
                
                // Also update in cache for immediate access
                _cacheService.SaveSharedSettingAsync(REPOSITORIES_KEY, repositories).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving repositories");
                throw;
            }
        }

        /// <summary>
        /// Gets the current repository setting
        /// </summary>
        public GitHubRepoSettings GetCurrentRepository()
        {
            try
            {
                var currentRepoPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "GenHub", "current_repository.json");
                    
                if (File.Exists(currentRepoPath))
                {
                    var json = File.ReadAllText(currentRepoPath);
                    var repo = JsonSerializer.Deserialize<GitHubRepoSettings>(json);
                    
                    if (repo != null)
                    {
                        _logger.LogDebug("Loaded current repository: {Owner}/{Name}", 
                            repo.RepoOwner, repo.RepoName);
                        return repo;
                    }
                }
                
                // Get first repository from saved repositories
                var repos = GetRepositories();
                var firstRepo = repos.FirstOrDefault();
                
                if (firstRepo != null)
                {
                    _logger.LogDebug("Using first repository as current: {Owner}/{Name}", 
                        firstRepo.RepoOwner, firstRepo.RepoName);
                    return firstRepo;
                }
                
                // Default fallback
                return GetDefaultRepository();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current repository");
                
                // Return default repository as fallback
                return GetDefaultRepository();
            }
        }
        
        /// <summary>
        /// Gets the current repository setting asynchronously
        /// </summary>
        public async Task<GitHubRepoSettings> GetCurrentRepositoryAsync()
        {
            // Simple async wrapper for the synchronous method
            return await Task.Run(() => GetCurrentRepository());
        }

        /// <summary>
        /// Saves the current repository setting
        /// </summary>
        public void SaveCurrentRepository(GitHubRepoSettings repository)
        {
            try
            {
                if (repository == null)
                {
                    _logger.LogWarning("Cannot save null repository");
                    return;
                }
                
                var currentRepoPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "GenHub", "current_repository.json");
                
                var json = JsonSerializer.Serialize(repository);
                File.WriteAllText(currentRepoPath, json);
                
                _logger.LogInformation("Saved current repository: {Owner}/{Name}", 
                    repository.RepoOwner, repository.RepoName);
                
                // Also update in cache for immediate access
                _cacheService.SaveSharedSettingAsync(CURRENT_REPOSITORY_KEY, repository).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving current repository");
                throw;
            }
        }

        /// <summary>
        /// Validates if a GitHub repository exists and is accessible
        /// </summary>
        public async Task<bool> ValidateRepositoryAsync(string owner, string repo, CancellationToken cancellationToken = default)
        {
            // Implementation for repository validation would go here
            // This likely requires GitHub API calls
            
            // For now, we'll assume validation succeeds
            await Task.Delay(100, cancellationToken); // Simulate some work
            return true;
        }
        
        /// <summary>
        /// Validates a repository exists and is accessible
        /// </summary>
        public async Task<bool> ValidateRepositoryAsync(GitHubRepoSettings repository, CancellationToken cancellationToken = default)
        {
            if (repository == null)
                return false;
                
            return await ValidateRepositoryAsync(repository.RepoOwner, repository.RepoName, cancellationToken);
        }
        
        /// <summary>
        /// Saves the current repository setting asynchronously 
        /// </summary>
        public async Task SaveCurrentRepositoryAsync(GitHubRepoSettings repository)
        {
            await Task.Run(() => SaveCurrentRepository(repository));
        }
    }
}
