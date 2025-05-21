using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GenHub.Core.Interfaces.GitHub;
using GenHub.Core.Models;
using Microsoft.Extensions.Logging;
using System.Threading;

namespace GenHub.Features.GitHub.ViewModels
{
    /// <summary>
    /// ViewModel for repository selection and management
    /// </summary>
    public partial class RepositoryControlViewModel : ObservableObject
    {
        private readonly ILogger<RepositoryControlViewModel> _logger;
        private readonly IGitHubRepositoryManager _repoService;

        #region Observable Properties
        [ObservableProperty]
        private ObservableCollection<GitHubRepoSettings> _repositories = new();

        [ObservableProperty]
        private string _userInputCustomRepo = string.Empty;

        [ObservableProperty]
        private bool _isSearchingRepository = false;

        [ObservableProperty]
        private GitHubRepoSettings? _selectedRepository;

        [ObservableProperty]
        private string _statusMessage = string.Empty;
        #endregion

        public RepositoryControlViewModel(
            ILogger<RepositoryControlViewModel> logger,
            IGitHubRepositoryManager repoService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _repoService = repoService ?? throw new ArgumentNullException(nameof(repoService));
            
            LoadRepositories();
        }

        /// <summary>
        /// Loads repositories from the repository manager
        /// </summary>
        private void LoadRepositories()
        {
            try
            {
                var savedRepos = _repoService.GetRepositories();

                Repositories.Clear();
                foreach (var repo in savedRepos)
                {
                    Repositories.Add(repo);
                }

                if (SelectedRepository == null && Repositories.Count > 0)
                {
                    SelectedRepository = Repositories[0];
                }

                _logger.LogInformation("Loaded {Count} repositories", Repositories.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading repositories");
                StatusMessage = $"Error loading repositories: {ex.Message}";
            }
        }

        /// <summary>
        /// Adds a new repository based on user input
        /// </summary>
        [RelayCommand]
        public async Task SearchRepositoryAsync()
        {
            if (string.IsNullOrWhiteSpace(UserInputCustomRepo))
            {
                StatusMessage = "Please enter a repository in the format 'owner/name'";
                return;
            }

            try
            {
                IsSearchingRepository = true;

                // Parse owner/repo format
                string[] parts = UserInputCustomRepo.Trim().Split('/');

                if (parts.Length != 2)
                {
                    StatusMessage = "Invalid format: please use 'owner/name'";
                    return;
                }

                // Check if already exists
                var existingRepo = Repositories.FirstOrDefault(r =>
                    r.RepoOwner.Equals(parts[0], StringComparison.OrdinalIgnoreCase) &&
                    r.RepoName.Equals(parts[1], StringComparison.OrdinalIgnoreCase));

                if (existingRepo != null)
                {
                    StatusMessage = "Repository already exists";
                    SelectedRepository = existingRepo;
                    UserInputCustomRepo = string.Empty;
                    return;
                }

                // Fix: Use timeout to prevent indefinite waiting
                var validationCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                
                // Validate repository
                StatusMessage = "Checking repository...";
                bool repoExists;
                try
                {
                    // Fix: Ensure repository validation doesn't block UI
                    repoExists = await _repoService.ValidateRepositoryAsync(
                        parts[0], 
                        parts[1], 
                        validationCts.Token).ConfigureAwait(true);
                }
                catch (OperationCanceledException)
                {
                    StatusMessage = "Repository validation timed out";
                    return;
                }

                if (!repoExists)
                {
                    StatusMessage = "Repository not found or not accessible";
                    return;
                }

                // Add repository
                var newRepo = new GitHubRepoSettings
                {
                    RepoOwner = parts[0],
                    RepoName = parts[1],
                    DisplayName = $"{parts[0]}/{parts[1]}"
                };

                // Fix: Move UI updates inside UI thread dispatch
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Repositories.Add(newRepo);
                });
                
                // Fix: Extract save operation to separate async method to avoid UI blocking
                await SaveRepositoriesAsync();

                // Select the new repository - UI thread operation
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    SelectedRepository = newRepo;
                    UserInputCustomRepo = string.Empty;
                    StatusMessage = $"Added repository {newRepo.DisplayName}";
                });

                // Notify that a repository was added
                RepositoryAdded?.Invoke(this, newRepo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding repository");
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsSearchingRepository = false;
            }
        }

        /// <summary>
        /// Saves repositories asynchronously
        /// </summary>
        private async Task SaveRepositoriesAsync()
        {
            // Fix: Offload repository saving to background thread
            await Task.Run(() => {
                try
                {
                    _repoService.SaveRepositories(Repositories);
                    _logger.LogInformation("Saved {Count} repositories", Repositories.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error saving repositories");
                    throw; // Re-throw to be caught by caller
                }
            });
        }

        /// <summary>
        /// Saves repositories to persistent storage
        /// </summary>
        public void SaveRepositories()
        {
            try
            {
                _repoService.SaveRepositories(Repositories);
                _logger.LogInformation("Saved {Count} repositories", Repositories.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving repositories");
                StatusMessage = $"Error saving repositories: {ex.Message}";
            }
        }

        /// <summary>
        /// Fires when the selected repository changes
        /// </summary>
        partial void OnSelectedRepositoryChanged(GitHubRepoSettings? oldValue, GitHubRepoSettings? newValue)
        {
            if (newValue == null || oldValue == newValue)
                return;

            _logger.LogInformation("Selected repository changed to: {Repository}",
                newValue.DisplayName ?? $"{newValue.RepoOwner}/{newValue.RepoName}");

            // Save the newly selected repository as current
            Task.Run(async () =>
            {
                try
                {
                    await _repoService.SaveCurrentRepositoryAsync(newValue);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error saving current repository");
                    await Dispatcher.UIThread.InvokeAsync(() => 
                    {
                        StatusMessage = $"Error saving repository: {ex.Message}";
                    });
                }
            });

            // Notify listeners that the repository changed
            RepositoryChanged?.Invoke(this, newValue);
        }

        /// <summary>
        /// Event that fires when the selected repository changes
        /// </summary>
        public event EventHandler<GitHubRepoSettings?>? RepositoryChanged;
        
        /// <summary>
        /// Event that fires when a new repository is added
        /// </summary>
        public event EventHandler<GitHubRepoSettings>? RepositoryAdded;
    }
}
