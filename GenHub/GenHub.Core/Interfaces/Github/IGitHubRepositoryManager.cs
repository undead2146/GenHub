using GenHub.Core.Models;
using GenHub.Core.Models.GitHub;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Models.Results;

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
        IEnumerable<GitHubRepository> GetRepositories();

        /// <summary>
        /// Gets the list of saved repositories asynchronously
        /// </summary>
        Task<IEnumerable<GitHubRepository>> GetRepositoriesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current repository setting synchronously
        /// </summary>
        GitHubRepository GetCurrentRepository();

        /// <summary>
        /// Gets the current repository setting asynchronously
        /// </summary>
        Task<GitHubRepository> GetCurrentRepositoryAsync();

        /// <summary>
        /// Gets the default repository setting
        /// </summary>
        GitHubRepository GetDefaultRepository();

        /// <summary>
        /// Saves the current repository setting synchronously
        /// </summary>
        void SaveCurrentRepository(GitHubRepository repository);
        
        /// <summary>
        /// Saves the current repository setting asynchronously 
        /// </summary>
        Task SaveCurrentRepositoryAsync(GitHubRepository repository);
        
        /// <summary>
        /// Saves the list of repositories
        /// </summary>
        void SaveRepositories(IEnumerable<GitHubRepository> repositories);
        
        /// <summary>
        /// Validates if a GitHub repository exists and is accessible
        /// </summary>
        Task<bool> ValidateRepositoryAsync(string owner, string repo, CancellationToken cancellationToken = default);
        
        Task<bool> ValidateRepositoryAsync(GitHubRepository repository, CancellationToken cancellationToken = default);

        /// <summary>
        /// Adds new repositories to the list or updates existing ones.
        /// </summary>
        /// <param name="repositoriesToAdd">The collection of repositories to add or update.</param>
        /// <param name="replaceExisting">If true, existing repositories will be updated with new data.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>An operation result indicating success or failure.</returns>
        Task<OperationResult> AddOrUpdateRepositoriesAsync(
            IEnumerable<GitHubRepository> repositoriesToAdd,
            bool replaceExisting = false,
            CancellationToken cancellationToken = default);
    }
}
