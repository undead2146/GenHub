using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Avalonia.Threading;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using GenHub.Core.Models.Enums;
using GenHub.Core.Interfaces.GitHub;
using GenHub.Core.Interfaces;
using GenHub.Core.Models;

namespace GenHub.Features.GitHub.ViewModels
{
    /// <summary>
    /// Orchestrator ViewModel for the GitHub manager window
    /// </summary>
    public partial class GitHubManagerViewModel : ObservableObject
    {
        private readonly ILogger<GitHubManagerViewModel> _logger;
        private readonly IGitHubTokenService _tokenService;
        private bool _isInitialized = false;

        #region Child ViewModels
        public RepositoryControlViewModel RepositoryControlVM { get; }
        public ContentModeFilterViewModel ContentModeFilterVM { get; }
        public GitHubItemsTreeViewModel GitHubItemsTreeVM { get; }
        public GitHubDetailsViewModel DetailsVM { get; }
        public InstallationViewModel InstallationVM { get; }
        #endregion

        #region Observable Properties
        [ObservableProperty]
        private bool _isLoading = false;

        [ObservableProperty]
        private string _statusMessage = "Ready";

        [ObservableProperty]
        private bool _showEmptyState = true;
        #endregion

        /// <summary>
        /// Event triggered when the window should be closed
        /// </summary>
        public event EventHandler? CloseRequested;

        /// <summary>
        /// Gets the GitHub service facade for backward compatibility
        /// </summary>
        public IGitHubServiceFacade? GitHubService { get; }

        public GitHubManagerViewModel(
            ILogger<GitHubManagerViewModel> logger,
            IGitHubTokenService tokenService,
            IGitHubServiceFacade gitHubService,
            RepositoryControlViewModel repositoryControlViewModel,
            ContentModeFilterViewModel contentModeFilterViewModel,
            GitHubItemsTreeViewModel gitHubItemsTreeViewModel,
            GitHubDetailsViewModel detailsViewModel,
            InstallationViewModel installationViewModel)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
            GitHubService = gitHubService ?? throw new ArgumentNullException(nameof(gitHubService));
            
            // Set child ViewModels
            RepositoryControlVM = repositoryControlViewModel ?? throw new ArgumentNullException(nameof(repositoryControlViewModel));
            ContentModeFilterVM = contentModeFilterViewModel ?? throw new ArgumentNullException(nameof(contentModeFilterViewModel));
            GitHubItemsTreeVM = gitHubItemsTreeViewModel ?? throw new ArgumentNullException(nameof(gitHubItemsTreeViewModel));
            DetailsVM = detailsViewModel ?? throw new ArgumentNullException(nameof(detailsViewModel));
            InstallationVM = installationViewModel ?? throw new ArgumentNullException(nameof(installationViewModel));
            
            // Set parent references for coordination
            GitHubItemsTreeVM.SetParentViewModel(this);
            
            // Subscribe to events from child ViewModels
            RepositoryControlVM.RepositoryChanged += OnRepositoryChanged;
            ContentModeFilterVM.WorkflowChanged += OnWorkflowChanged;
            ContentModeFilterVM.DisplayModeChanged += OnDisplayModeChanged;
            GitHubItemsTreeVM.ItemSelected += OnItemSelected;
            InstallationVM.InstallationCompleted += OnInstallationCompleted;
            
            // Subscribe to token events
            _tokenService.TokenMissing += OnTokenMissing;
            _tokenService.TokenInvalid += OnTokenInvalid;
            
            _logger.LogDebug("GitHubManagerViewModel created with all child ViewModels");
        }

        #region Event Handlers
        /// <summary>
        /// Handles repository selection changes
        /// </summary>
        private async void OnRepositoryChanged(object? sender, GitHubRepository? repository)
        {
            if (repository == null) 
            {
                _logger.LogWarning("Repository changed to null - skipping");
                return;
            }
            
            _logger.LogInformation("Repository changed to: {RepoOwner}/{RepoName}", repository.RepoOwner, repository.RepoName);
            
            try
            {
                // Update the repository in GitHubItemsTreeVM
                GitHubItemsTreeVM.SetRepository(repository);
                
                // Show loading indicator
                IsLoading = true;
                StatusMessage = $"Loading data for {repository.DisplayName}...";
                
                // Clear details and reset search state
                DetailsVM.SetSelectedItem(null);
                
                _logger.LogDebug("Loading workflow files for repository: {RepoName}", repository.DisplayName);
                
                // Load workflow files for the repository
                await ContentModeFilterVM.LoadWorkflowFilesForRepositoryAsync(repository)
                    .ConfigureAwait(false);
                
                _logger.LogDebug("Workflow files loaded, available workflows: {Count}", 
                    ContentModeFilterVM.AvailableWorkflows.Count);
                
                // Switch back to UI thread for UI updates
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    // Update empty state
                    ShowEmptyState = GitHubItemsTreeVM.ShowEmptyState;
                    StatusMessage = "Repository loaded successfully";
                });
                
                // Load initial content
                _logger.LogDebug("Loading initial content for repository");
                await GitHubItemsTreeVM.LoadContentAsync();
                
                _logger.LogInformation("Repository change completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling repository change for {RepoName}", repository.DisplayName);
                
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    StatusMessage = $"Error loading repository: {ex.Message}";
                    ShowEmptyState = true;
                });
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Handles workflow selection changes
        /// </summary>
        private async void OnWorkflowChanged(object? sender, WorkflowDefinitionViewModel? workflow)
        {
            if (!_isInitialized) return;
            
            _logger.LogInformation("Workflow changed to: {WorkflowName}", workflow?.Name ?? "All Items");
            
            // Update workflow context in GitHubItemsTreeVM
            GitHubItemsTreeVM.SetWorkflowContext(workflow, ContentModeFilterVM.CurrentDisplayMode);
            
            // Clear details when workflow changes
            DetailsVM.SetSelectedItem(null);
            
            // Load content based on the new workflow
            try
            {
                await GitHubItemsTreeVM.LoadContentAsync(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading content after workflow change");
                StatusMessage = $"Error loading content: {ex.Message}";
            }
        }

        /// <summary>
        /// Handles display mode changes
        /// </summary>
        private void OnDisplayModeChanged(object? sender, DisplayMode displayMode)
        {
            if (!_isInitialized) return;
            
            _logger.LogInformation("Display mode changed to: {DisplayMode}", displayMode);
            
            // Update display mode in GitHubItemsTreeVM
            GitHubItemsTreeVM.SetWorkflowContext(ContentModeFilterVM.SelectedWorkflow, displayMode);
            
            // Clear details when display mode changes
            DetailsVM.SetSelectedItem(null);
            
            // Load content based on the new display mode
            _ = GitHubItemsTreeVM.LoadContentAsync(true);
        }

        /// <summary>
        /// Handles item selection changes
        /// </summary>
        private void OnItemSelected(object? sender, IGitHubDisplayItem? item)
        {
            try
            {
                _logger.LogDebug("Item selected in manager: {DisplayName} (Type: {Type})", 
                    item?.DisplayName ?? "null", item?.GetType().Name ?? "null");

                // Update details view with selected item
                DetailsVM.SetSelectedItem(item);
                
                // Update installation view based on item type
                if (item is GitHubArtifactDisplayItemViewModel artifactVM)
                {
                    InstallationVM.SetSelectedItem(artifactVM);
                    _logger.LogDebug("Set artifact for installation: {ArtifactName}", artifactVM.DisplayName);
                }
                else if (item is GitHubReleaseDisplayItemViewModel releaseVM)
                {
                    InstallationVM.SetSelectedItem(releaseVM);
                    _logger.LogDebug("Set release for installation: {ReleaseName}", releaseVM.DisplayName);
                }
                else
                {
                    InstallationVM.SetSelectedItem(null);
                    _logger.LogDebug("Cleared installation selection - item type not installable");
                }
                
                // Update empty state
                ShowEmptyState = item == null && GitHubItemsTreeVM.ShowEmptyState;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling item selection");
                StatusMessage = $"Error selecting item: {ex.Message}";
            }
        }
        
        /// <summary>
        /// Handles installation completion events
        /// </summary>
        private void OnInstallationCompleted(object? sender, InstallationCompletedEventArgs e)
        {
            if (e.Success)
            {
                StatusMessage = "Installation completed successfully";
                _logger.LogInformation("Installation completed for item: {ItemName}", 
                    e.InstalledItem?.DisplayName ?? "Unknown");
            }
            else
            {
                StatusMessage = $"Installation failed: {e.Message}";
                _logger.LogError("Installation failed: {ErrorMessage}", e.Message);
            }
        }

        /// <summary>
        /// Handles missing token events
        /// </summary>
        private void OnTokenMissing(object? sender, EventArgs e)
        {
            StatusMessage = "GitHub token is missing. Please configure your token.";
            _logger.LogWarning("GitHub token is missing");
        }

        /// <summary>
        /// Handles invalid token events
        /// </summary>
        private void OnTokenInvalid(object? sender, EventArgs e)
        {
            StatusMessage = "GitHub token is invalid. Please update your token.";
            _logger.LogWarning("GitHub token is invalid");
        }
        #endregion

        #region Commands
        /// <summary>
        /// Initializes the view model and loads initial data
        /// </summary>
        [RelayCommand]
        public async Task InitializeAsync()
        {
            if (_isInitialized)
                return;

            try
            {
                IsLoading = true;
                StatusMessage = "Initializing...";
                ShowEmptyState = true;

                _logger.LogDebug("Starting GitHubManagerViewModel initialization");

                // Ensure repositories are loaded first
                var currentRepository = RepositoryControlVM.SelectedRepository;
                if (currentRepository == null && RepositoryControlVM.Repositories.Any())
                {
                    RepositoryControlVM.SelectedRepository = RepositoryControlVM.Repositories.First();
                    currentRepository = RepositoryControlVM.SelectedRepository;
                }

                if (currentRepository != null)
                {
                    _logger.LogDebug("Initializing with repository: {RepoName}", currentRepository.DisplayName);
                    
                    // Set the repository in GitHubItemsTreeVM
                    GitHubItemsTreeVM.SetRepository(currentRepository);
                    
                    // Load workflow files for the repository
                    await ContentModeFilterVM.LoadWorkflowFilesForRepositoryAsync(currentRepository);
                    
                    // Set the workflow context in GitHubItemsTreeVM
                    GitHubItemsTreeVM.SetWorkflowContext(
                        ContentModeFilterVM.SelectedWorkflow,
                        ContentModeFilterVM.CurrentDisplayMode);
                    
                    // Load initial content
                    await GitHubItemsTreeVM.LoadContentAsync(true);
                }
                else
                {
                    _logger.LogWarning("No repositories available for initialization");
                    StatusMessage = "No repositories configured";
                }

                _isInitialized = true;
                StatusMessage = "Ready";
                ShowEmptyState = GitHubItemsTreeVM.ShowEmptyState;
                
                _logger.LogInformation("GitHubManagerViewModel initialization completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing GitHubManagerViewModel");
                StatusMessage = $"Error loading data: {ex.Message}";
                ShowEmptyState = true;
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Handles the initial UI setup after view is loaded
        /// </summary>
        [RelayCommand]
        public async Task ViewLoadedAsync()
        {
            try
            {
                if (!_isInitialized)
                {
                    await InitializeAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ViewLoadedAsync");
                StatusMessage = $"Error loading view: {ex.Message}";
            }
        }

        /// <summary>
        /// Refreshes the content in the tree view
        /// </summary>
        [RelayCommand]
        public async Task RefreshAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Refreshing...";
                
                await GitHubItemsTreeVM.RefreshAsync();
                
                StatusMessage = "Refresh complete";
                ShowEmptyState = GitHubItemsTreeVM.ShowEmptyState;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing");
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }
        
        /// <summary>
        /// Configures the API token using the token service
        /// </summary>
        [RelayCommand]
        public async Task ConfigureTokenAsync()
        {
            try
            {
                StatusMessage = "Configuring token...";
                _logger.LogInformation("Starting token configuration");

                // Use the token service to handle the configuration
                var (success, token) = await _tokenService.ConfigureTokenAsync();

                if (success && !string.IsNullOrEmpty(token))
                {
                    StatusMessage = "Token configured successfully";
                    _logger.LogInformation("Token configuration completed successfully");
                    
                    // Refresh content with the new token
                    await RefreshAsync();
                }
                else
                {
                    StatusMessage = "Token configuration cancelled or failed";
                    _logger.LogWarning("Token configuration was cancelled or failed");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error configuring token");
                StatusMessage = $"Error configuring token: {ex.Message}";
            }
        }

        /// <summary>
        /// Closes the window
        /// </summary>
        [RelayCommand]
        public void CloseWindow()
        {
            try
            {
                _logger.LogDebug("CloseWindow command executed");
                CloseRequested?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error closing window");
            }
        }

        /// <summary>
        /// Performs a search using the current criteria
        /// </summary>
        [RelayCommand]
        public async Task SearchAsync()
        {
            try
            {
                _logger.LogInformation("Search command executed from manager");
                
                // Delegate to GitHubItemsTreeVM
                await GitHubItemsTreeVM.SearchAsync();
                
                // Update empty state based on search results
                ShowEmptyState = GitHubItemsTreeVM.ShowEmptyState;
                
                // Update status message if search was performed
                if (GitHubItemsTreeVM.IsSearchActive)
                {
                    StatusMessage = $"Search completed: {GitHubItemsTreeVM.SearchResultCount} results";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing search command");
                StatusMessage = $"Search error: {ex.Message}";
            }
        }

        /// <summary>
        /// Clears the current search
        /// </summary>
        [RelayCommand]
        public async Task ClearSearchAsync()
        {
            try
            {
                _logger.LogInformation("Clear search command executed from manager");
                
                // Delegate to GitHubItemsTreeVM
                await GitHubItemsTreeVM.ClearSearchAsync();
                
                // Update empty state
                ShowEmptyState = GitHubItemsTreeVM.ShowEmptyState;
                StatusMessage = "Search cleared";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing clear search command");
                StatusMessage = $"Error clearing search: {ex.Message}";
            }
        }
        #endregion

        /// <summary>
        /// Cleans up resources when the ViewModel is disposed
        /// </summary>
        public void Cleanup()
        {
            // Unsubscribe from all events
            RepositoryControlVM.RepositoryChanged -= OnRepositoryChanged;
            ContentModeFilterVM.WorkflowChanged -= OnWorkflowChanged;
            ContentModeFilterVM.DisplayModeChanged -= OnDisplayModeChanged;
            GitHubItemsTreeVM.ItemSelected -= OnItemSelected;
            InstallationVM.InstallationCompleted -= OnInstallationCompleted;
            
            _tokenService.TokenMissing -= OnTokenMissing;
            _tokenService.TokenInvalid -= OnTokenInvalid;
        }
    }

    /// <summary>
    /// Extension methods for fire-and-forget task execution
    /// </summary>
    public static class TaskExtensions
    {
        /// <summary>
        /// Executes a task in fire-and-forget manner with proper error handling
        /// </summary>
        public static async void FireAndForget(this Task task, ILogger logger)
        {
            try
            {
                await task;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in fire-and-forget task");
            }
        }
    }
}
