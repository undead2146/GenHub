using GenHub.Core.Models;
using GenHub.Core.Models.GitHub;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GenHub.Core.Interfaces.GitHub
{
    /// <summary>
    /// Interface for managing GitHub repositories
    /// </summary>
    public interface IGitHubRepositoryManager
    {
        /// <summary>
        /// Gets the list of saved repositories synchronously
        /// </summary>
        IEnumerable<GitHubRepoSettings> GetRepositories();

        /// <summary>
        /// Gets the list of saved repositories asynchronously
        /// </summary>
        Task<IEnumerable<GitHubRepoSettings>> GetRepositoriesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current repository setting synchronously
        /// </summary>
        GitHubRepoSettings GetCurrentRepository();

        /// <summary>
        /// Gets the current repository setting asynchronously
        /// </summary>
        Task<GitHubRepoSettings> GetCurrentRepositoryAsync();

        /// <summary>
        /// Gets the default repository setting
        /// </summary>
        GitHubRepoSettings GetDefaultRepository();

        /// <summary>
        /// Saves the current repository setting synchronously
        /// </summary>
        void SaveCurrentRepository(GitHubRepoSettings repository);
        
        /// <summary>
        /// Saves the current repository setting asynchronously 
        /// </summary>
        Task SaveCurrentRepositoryAsync(GitHubRepoSettings repository);
        
        /// <summary>
        /// Saves the list of repositories
        /// </summary>
        void SaveRepositories(IEnumerable<GitHubRepoSettings> repositories);
        
        /// <summary>
        /// Validates if a GitHub repository exists and is accessible
        /// </summary>
        Task<bool> ValidateRepositoryAsync(string owner, string repo, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Validates a repository exists and is accessible
        /// </summary>
        Task<bool> ValidateRepositoryAsync(GitHubRepoSettings repository, CancellationToken cancellationToken = default);
    }
}
