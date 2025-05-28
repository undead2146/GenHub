using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Avalonia.Threading;
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
            
            // Subscribe to events from child ViewModels
            RepositoryControlVM.RepositoryChanged += OnRepositoryChanged;
            ContentModeFilterVM.WorkflowChanged += OnWorkflowChanged;
            ContentModeFilterVM.DisplayModeChanged += OnDisplayModeChanged;
            GitHubItemsTreeVM.ItemSelected += OnItemSelected;
            InstallationVM.InstallationCompleted += OnInstallationCompleted;
            
            // Subscribe to token events
            _tokenService.TokenMissing += TokenMissingHandler;
            _tokenService.TokenInvalid += TokenInvalidHandler;
        }

        #region Event Handlers
        /// <summary>
        /// Handles repository selection changes
        /// </summary>
        private async void OnRepositoryChanged(object? sender, GitHubRepoSettings? repository)
        {
            if (repository == null) return;
            
            try
            {
                // Update the repository in GitHubItemsTreeVM
                GitHubItemsTreeVM.SetRepository(repository);
                
                // Show loading indicator
                IsLoading = true;
                StatusMessage = $"Loading data for {repository.DisplayName}...";
                
                // Clear details
                DetailsVM.SetSelectedItem(null);
                
                // Load workflow files for the repository
                await ContentModeFilterVM.LoadWorkflowFilesForRepositoryAsync(repository)
                    .ConfigureAwait(false);
                
                // Switch back to UI thread for UI updates
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    GitHubItemsTreeVM.SetWorkflowContext(
                        ContentModeFilterVM.SelectedWorkflow,
                        ContentModeFilterVM.CurrentDisplayMode);
                    
                    // Load repository content asynchronously
                    LoadRepositoryContentAsync(repository).FireAndForget(_logger);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling repository change");
                
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    StatusMessage = $"Error: {ex.Message}";
                    IsLoading = false;
                });
            }
        }

        /// <summary>
        /// Helper method to load repository content and handle loading state
        /// </summary>
        private async Task LoadRepositoryContentAsync(GitHubRepoSettings repository)
        {
            try
            {
                // Load content based on the selected workflow and display mode
                await GitHubItemsTreeVM.LoadContentAsync(true);
                
                // Update status when done
                StatusMessage = $"Loaded data for {repository.DisplayName}";
                ShowEmptyState = GitHubItemsTreeVM.ShowEmptyState;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading repository content");
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                // Always turn off loading indicator when done
                IsLoading = false;
            }
        }

        /// <summary>
        /// Handles workflow selection changes
        /// </summary>
        private void OnWorkflowChanged(object? sender, WorkflowDefinitionViewModel? workflow)
        {
            if (!_isInitialized) return;
            
            // Update workflow context in GitHubItemsTreeVM
            GitHubItemsTreeVM.SetWorkflowContext(workflow, ContentModeFilterVM.CurrentDisplayMode);
            
            // Load content based on the new workflow
            _ = GitHubItemsTreeVM.LoadContentAsync(true);
        }

        /// <summary>
        /// Handles display mode changes
        /// </summary>
        private void OnDisplayModeChanged(object? sender, DisplayMode displayMode)
        {
            if (!_isInitialized) return;
            
            // Update display mode in GitHubItemsTreeVM
            GitHubItemsTreeVM.SetWorkflowContext(ContentModeFilterVM.SelectedWorkflow, displayMode);
            
            // Load content based on the new display mode
            _ = GitHubItemsTreeVM.LoadContentAsync(true);
        }

        /// <summary>
        /// Handles item selection changes
        /// </summary>
        private void OnItemSelected(object? sender, IGitHubDisplayItem? item)
        {
            // Update details view with selected item
            DetailsVM.SetSelectedItem(item);
            
            // Update installation VM with the selected artifact
            if (item is GitHubArtifactDisplayItemViewModel artifactVM)
            {
                InstallationVM.SetCurrentArtifact(artifactVM.Artifact);
            }
            else
            {
                InstallationVM.SetCurrentArtifact(null);
            }
            
            // Update empty state
            ShowEmptyState = GitHubItemsTreeVM.ShowEmptyState;
        }
        
        /// <summary>
        /// Handles installation completion events
        /// </summary>
        private void OnInstallationCompleted(object? sender, InstallationEventArgs args)
        {
            if (args.Success)
            {
                StatusMessage = $"Successfully installed {args.Artifact?.Name}";
            }
            else if (args.Cancelled)
            {
                StatusMessage = "Installation cancelled";
            }
            else
            {
                StatusMessage = args.Error != null ? 
                    $"Installation error: {args.Error}" : 
                    "Installation failed";
            }
            
            // Refresh the tree view to update installation status
            _ = GitHubItemsTreeVM.RefreshAsync();
        }
        
        /// <summary>
        /// Event handler for when a GitHub token is missing
        /// </summary>
        private async void TokenMissingHandler(object? sender, EventArgs e)
        {
            _logger.LogWarning("GitHub token is missing - prompting user to configure");
            StatusMessage = "GitHub token is required. Please configure your token.";
            await ConfigureTokenAsync();
        }

        /// <summary>
        /// Event handler for when a GitHub token is invalid
        /// </summary>
        private async void TokenInvalidHandler(object? sender, EventArgs e)
        {
            _logger.LogWarning("GitHub token is invalid - prompting user to configure");
            StatusMessage = "GitHub token is invalid or expired. Please reconfigure your token.";
            await ConfigureTokenAsync();
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

                // First get the initial repository from RepositoryControlVM
                var currentRepository = RepositoryControlVM.SelectedRepository;
                if (currentRepository != null)
                {
                    // Set the repository in GitHubItemsTreeVM
                    GitHubItemsTreeVM.SetRepository(currentRepository);
                    
                    // Load workflow files for the repository
                    await ContentModeFilterVM.LoadWorkflowFilesForRepositoryAsync(currentRepository);
                    
                    // Set the workflow context in GitHubItemsTreeVM
                    GitHubItemsTreeVM.SetWorkflowContext(
                        ContentModeFilterVM.SelectedWorkflow,
                        ContentModeFilterVM.CurrentDisplayMode);
                    
                    // Load content
                    await GitHubItemsTreeVM.LoadContentAsync(true);
                }

                _isInitialized = true;
                StatusMessage = "Ready";
                ShowEmptyState = GitHubItemsTreeVM.ShowEmptyState;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing");
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
            
            _tokenService.TokenMissing -= TokenMissingHandler;
            _tokenService.TokenInvalid -= TokenInvalidHandler;
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
