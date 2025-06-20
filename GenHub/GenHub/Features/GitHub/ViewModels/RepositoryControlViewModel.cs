using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using GenHub.Core.Interfaces.GitHub;
using GenHub.Core.Models;
using GenHub.Core.Models.GitHub;
using GenHub.Core.Interfaces;
using Avalonia.Threading;
using System.Threading;

namespace GenHub.Features.GitHub.ViewModels
{
    /// <summary>
    /// ViewModel for repository control operations
    /// </summary>
    public partial class RepositoryControlViewModel : ObservableObject
    {
        private readonly ILogger<RepositoryControlViewModel> _logger;
        private readonly IGitHubRepositoryManager _repoService;
        private readonly IGitHubServiceFacade _gitHubService;

        [ObservableProperty]
        private ObservableCollection<GitHubRepository> repositories = new();

        [ObservableProperty]
        private GitHubRepository? selectedRepository;

        [ObservableProperty]
        private string statusMessage = string.Empty;

        [ObservableProperty] 
        private bool isLoading;

        [ObservableProperty]
        private bool isDiscovering;

        public event EventHandler<GitHubRepository?>? RepositoryChanged;

        public RepositoryControlViewModel(
            ILogger<RepositoryControlViewModel> logger,
            IGitHubRepositoryManager repoService,
            IGitHubServiceFacade gitHubService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _repoService = repoService ?? throw new ArgumentNullException(nameof(repoService));
            _gitHubService = gitHubService ?? throw new ArgumentNullException(nameof(gitHubService));
            
            _logger.LogDebug("RepositoryControlViewModel constructor started");
            
            // Initialize immediately
            _ = Task.Run(async () => await InitializeAsync());
        }

        private async Task InitializeAsync()
        {
            try
            {
                _logger.LogDebug("Starting repository initialization");
                IsLoading = true;
                StatusMessage = "Loading repositories...";

                await LoadRepositoriesAsync();
                
                _logger.LogInformation("Repository initialization completed. Total repositories: {Count}", Repositories.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during repository initialization");
                StatusMessage = $"Error loading repositories: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadRepositoriesAsync()
        {
            try
            {
                _logger.LogDebug("Loading repositories from service");
                
                var savedRepos = await Task.Run(() => _repoService.GetRepositories());
                
                _logger.LogDebug("Retrieved {Count} repositories from service", savedRepos?.Count() ?? 0);

                // Switch to UI thread for collection updates
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Repositories.Clear();
                    
                    if (savedRepos != null)
                    {
                        foreach (var repo in savedRepos)
                        {
                            // Validate repository before adding
                            if (repo.IsValid)
                            {
                                Repositories.Add(repo);
                                _logger.LogDebug("Added repository: {DisplayName}", repo.ComputedDisplayName);
                            }
                            else
                            {
                                _logger.LogWarning("Skipped invalid repository: Owner='{Owner}', Name='{Name}'", 
                                    repo.RepoOwner, repo.RepoName);
                            }
                        }
                    }

                    // Set initial selection
                    SetInitialSelection();
                    
                    StatusMessage = $"Loaded {Repositories.Count} repositories";
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading repositories");
                throw;
            }
        }

        private void SetInitialSelection()
        {
            try
            {
                if (SelectedRepository == null && Repositories.Count > 0)
                {
                    var defaultRepo = Repositories.First();

                    OnPropertyChanged(nameof(SelectedRepository));
                    
                    _logger.LogInformation("Initial repository selected: {DisplayName}", defaultRepo.ComputedDisplayName);
                }
                else if (SelectedRepository != null)
                {
                    _logger.LogDebug("Repository already selected: {DisplayName}", SelectedRepository.ComputedDisplayName);
                }
                else
                {
                    _logger.LogWarning("No repositories available for selection");
                    StatusMessage = "No repositories configured";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting initial repository selection");
            }
        }

        partial void OnSelectedRepositoryChanged(GitHubRepository? value)
        {
            try
            {
                _logger.LogDebug("Repository selection changed to: {DisplayName}", 
                    value?.ComputedDisplayName ?? "null");
                    
                RepositoryChanged?.Invoke(this, value);
                
                if (value != null)
                {
                    StatusMessage = $"Selected: {value.ComputedDisplayName}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling repository selection change");
            }
        }

        [RelayCommand]
        private async Task RefreshRepositoriesAsync()
        {
            _logger.LogDebug("Refreshing repositories");
            await LoadRepositoriesAsync();
        }


        /// <summary>
        /// Discovers and adds C&C repositories
        /// </summary>
        [RelayCommand]
        private async Task DiscoverRepositories()
        {
            if (IsDiscovering) return;

            try
            {
                IsDiscovering = true;
                _logger.LogInformation("Starting repository discovery");

                // Run discovery on background thread to prevent UI freezing
                var discoveryTask = Task.Run(async () =>
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5)); // 5 minute timeout
                    
                    var result = await _gitHubService.RepositoryDiscovery.DiscoverRepositoriesAsync(
                        new DiscoveryOptions { MaxResultsToReturn = 50 }, 
                        cts.Token);
                        
                    return result;
                });

                var result = await discoveryTask;

                if (result.Success && result.Data?.Any() == true)
                {
                    var repositoriesList = result.Data.ToList();
                    _logger.LogInformation("Discovered {Count} repositories", repositoriesList.Count);

                    // Add to repository manager
                    await _gitHubService.RepositoryDiscovery.AddDiscoveredRepositoriesAsync(repositoriesList, replaceExisting: false);

                    // Refresh the repositories list
                    await LoadRepositoriesAsync();

                    // Select the first discovered repository if none selected
                    if (SelectedRepository == null && Repositories.Any())
                    {
                        SelectedRepository = Repositories.First();
                        _logger.LogInformation("Initial repository selected: {DisplayName}", SelectedRepository.ComputedDisplayName);
                    }

                    _logger.LogInformation("Successfully added {Count} discovered repositories", repositoriesList.Count);
                }
                else
                {
                    _logger.LogWarning("Repository discovery failed or returned no results: {Error}", 
                        result.Message ?? "Unknown error");
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Repository discovery was cancelled due to timeout");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during repository discovery");
            }
            finally
            {
                IsDiscovering = false;
            }
        }

        [RelayCommand]
        private async Task AddRepositoryAsync()
        {
            // Implementation for adding new repository
            _logger.LogDebug("Add repository command triggered");
            await Task.CompletedTask; // Placeholder implementation
        }
    }
}
