using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GenHub.Core.Interfaces.GitHub;
using GenHub.Core.Models;
using GenHub.Core.Models.Results;
using GenHub.Core.Models.GameProfiles;
using Microsoft.Extensions.Logging;
using GenHub.Core.Interfaces;

namespace GenHub.Features.GitHub.ViewModels
{
    /// <summary>
    /// ViewModel for GitHub item details display and installation management
    /// </summary>
    public partial class GitHubDetailsViewModel : ObservableObject
    {
        private readonly ILogger<GitHubDetailsViewModel> _logger;
        private readonly IGitHubServiceFacade _gitHubService;

        #region Observable Properties
        [ObservableProperty]
        private GitHubArtifactDisplayItemViewModel? _selectedArtifact;

        [ObservableProperty]
        private GitHubWorkflowDisplayItemViewModel? _selectedWorkflow;

        [ObservableProperty]
        private GitHubReleaseDisplayItemViewModel? _selectedRelease;

        [ObservableProperty]
        private string _selectedArtifactSizeFormatted = string.Empty;

        [ObservableProperty]
        private GitHubWorkflowDisplayItemViewModel? _selectedWorkflowGroup;

        [ObservableProperty]
        private ObservableCollection<IGitHubDisplayItem> _workflowRunItems = new();

        [ObservableProperty]
        private bool _isInstalling = false;

        [ObservableProperty]
        private double _installProgress = 0;

        [ObservableProperty]
        private string _installStatusMessage = string.Empty;

        [ObservableProperty]
        private GitHubArtifact? _currentlyInstallingArtifact;

        [ObservableProperty]
        private IGitHubDisplayItem? _selectedItem;
        #endregion

        #region Computed Properties
        /// <summary>
        /// Gets a value indicating whether any item is currently selected
        /// </summary>
        public bool HasSelection => SelectedGitHubItem != null || SelectedArtifact != null;

        /// <summary>
        /// Gets a value indicating whether an artifact is currently selected
        /// </summary>
        public bool HasSelectedArtifact => SelectedArtifact != null;

        /// <summary>
        /// Gets a value indicating whether a release is currently selected
        /// </summary>
        public bool HasSelectedRelease => SelectedGitHubItem is GitHubReleaseDisplayItemViewModel;

        /// <summary>
        /// Gets a value indicating whether a workflow is currently selected
        /// </summary>
        public bool HasSelectedWorkflow => SelectedGitHubItem is GitHubWorkflowDisplayItemViewModel;
        #endregion

        /// <summary>
        /// The currently selected GitHub item
        /// </summary>
        public IGitHubDisplayItem? SelectedGitHubItem { get; private set; }

        public GitHubDetailsViewModel(
            ILogger<GitHubDetailsViewModel> logger,
            IGitHubServiceFacade gitHubService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _gitHubService = gitHubService ?? throw new ArgumentNullException(nameof(gitHubService));
        }

        /// <summary>
        /// Sets the selected item and updates the details view accordingly
        /// </summary>
        /// <param name="item">The item to select, or null to clear selection</param>
        public void SetSelectedItem(IGitHubDisplayItem? item)
        {
            try
            {
                _logger.LogDebug("Setting selected item to {Type}", item?.GetType().Name ?? "null");
                SelectedGitHubItem = item;

                // Clear previous selections
                SelectedArtifact = null;
                SelectedWorkflowGroup = null;
                SelectedRelease = null;
                WorkflowRunItems.Clear();

                if (item == null) return;

                // Handle different item types
                switch (item)
                {
                    case GitHubArtifactDisplayItemViewModel artifactViewModel:
                        HandleArtifactSelection(artifactViewModel);
                        break;
                    case GitHubWorkflowDisplayItemViewModel workflow:
                        HandleWorkflowSelection(workflow);
                        break;
                    case GitHubReleaseDisplayItemViewModel release:
                        HandleReleaseSelection(release);
                        break;
                }

                // Notify property changes for computed properties
                OnPropertyChanged(nameof(HasSelection));
                OnPropertyChanged(nameof(HasSelectedArtifact));
                OnPropertyChanged(nameof(HasSelectedRelease));
                OnPropertyChanged(nameof(HasSelectedWorkflow));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting selected item");
            }
        }

        /// <summary>
        /// Handles selection of an artifact item
        /// </summary>
        private void HandleArtifactSelection(GitHubArtifactDisplayItemViewModel artifactViewModel)
        {
            SelectedArtifact = artifactViewModel;
            SelectedArtifactSizeFormatted = FormatFileSize(artifactViewModel.SizeInBytes);
            _logger.LogDebug("Selected artifact: {Name} ({Size})", artifactViewModel.Name, SelectedArtifactSizeFormatted);
        }

        /// <summary>
        /// Handles selection of a workflow item
        /// </summary>
        private void HandleWorkflowSelection(GitHubWorkflowDisplayItemViewModel workflow)
        {
            SelectedWorkflowGroup = workflow;
            _logger.LogDebug("Selected workflow: {Name}", workflow.DisplayName);

            // Load artifacts asynchronously
            _ = Task.Run(async () =>
            {
                await workflow.LoadChildrenAsync();
                await UpdateWorkflowRunItemsFromWorkflowAsync(workflow);
            });
        }

        /// <summary>
        /// Handles selection of a release item
        /// </summary>
        private void HandleReleaseSelection(GitHubReleaseDisplayItemViewModel release)
        {
            SelectedRelease = release;
            _logger.LogDebug("Selected release: {Name}", release.DisplayName);

            // Load assets asynchronously
            _ = Task.Run(async () =>
            {
                await release.LoadChildrenAsync();
                await UpdateWorkflowRunItemsFromReleaseAsync(release);
            });
        }

        /// <summary>
        /// Updates the workflow run items collection with artifacts from the selected workflow
        /// </summary>
        private async Task UpdateWorkflowRunItemsFromWorkflowAsync(GitHubWorkflowDisplayItemViewModel workflow)
        {
            try
            {
                // Allow time for async loading to complete
                await Task.Delay(100);
                
                // Update collection on UI thread
                WorkflowRunItems.Clear();
                foreach (var item in workflow.Artifacts)
                {
                    WorkflowRunItems.Add(item);
                }
                
                _logger.LogDebug("Updated workflow items: {Count} artifacts", WorkflowRunItems.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating workflow run items");
            }
        }

        /// <summary>
        /// Updates the workflow run items collection with assets from the selected release
        /// </summary>
        private async Task UpdateWorkflowRunItemsFromReleaseAsync(GitHubReleaseDisplayItemViewModel release)
        {
            try
            {
                // Allow time for async loading to complete
                await Task.Delay(100);
                
                // Update collection on UI thread
                WorkflowRunItems.Clear();
                foreach (var asset in release.Assets)
                {
                    WorkflowRunItems.Add(asset);
                }
                
                _logger.LogDebug("Updated release items: {Count} assets", WorkflowRunItems.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating release assets");
            }
        }

        /// <summary>
        /// Installs the currently selected artifact
        /// </summary>
        [RelayCommand]
        private async Task InstallSelectedArtifactAsync()
        {
            var artifactToInstall = SelectedArtifact?.Artifact;
            
            if (artifactToInstall == null || IsInstalling)
                return;

            try
            {
                IsInstalling = true;
                InstallProgress = 0;
                InstallStatusMessage = "Starting installation...";
                CurrentlyInstallingArtifact = artifactToInstall;

                _logger.LogInformation("Starting installation of artifact: {ArtifactName}", artifactToInstall.Name);

                var startTime = DateTime.Now;

                // Create progress tracker
                var progress = new Progress<InstallProgress>(p =>
                {
                    InstallProgress = p.Percentage;
                    InstallStatusMessage = p.Message;
                });

                // Install the artifact
                var gameVersion = await _gitHubService.InstallArtifactAsync(artifactToInstall, progress);

                var installationDuration = DateTime.Now - startTime;

                InstallationResult result;
                if (gameVersion != null)
                {
                    InstallStatusMessage = "Installation successful!";
                    InstallProgress = 1;

                    // Update artifact status
                    artifactToInstall.IsInstalled = true;
                    if (SelectedArtifact != null)
                    {
                        SelectedArtifact.IsInstalled = true; 
                    }

                    _logger.LogInformation("Successfully installed artifact: {ArtifactName} as {VersionName}", 
                        artifactToInstall.Name, gameVersion.Name);

                    result = InstallationResult.Succeeded(
                        artifactToInstall, 
                        gameVersion, 
                        gameVersion.InstallPath,
                        artifactToInstall.SizeInBytes,
                        installationDuration);
                }
                else
                {
                    InstallStatusMessage = "Installation failed";
                    _logger.LogWarning("Installation failed for artifact: {ArtifactName}", artifactToInstall.Name);
                    
                    result = InstallationResult.Failed(
                        "Installation completed but no game version was created",
                        artifactToInstall);
                }
                
                // Notify installation result
                InstallationCompleted?.Invoke(this, result);
            }
            catch (OperationCanceledException)
            {
                InstallStatusMessage = "Installation cancelled";
                _logger.LogInformation("Installation cancelled for artifact: {ArtifactName}", artifactToInstall?.Name);
                
                var cancelledResult = InstallationResult.Failed(
                    "Installation was cancelled", 
                    artifactToInstall);
                InstallationCompleted?.Invoke(this, cancelledResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error installing artifact: {ArtifactName}", artifactToInstall?.Name);
                InstallStatusMessage = $"Error: {ex.Message}";
                
                var errorResult = InstallationResult.Failed(
                    ex.Message, 
                    artifactToInstall, 
                    ex);
                InstallationCompleted?.Invoke(this, errorResult);
            }
            finally
            {
                IsInstalling = false;
                CurrentlyInstallingArtifact = null;
            }
        }

        /// <summary>
        /// Cancels the current installation operation
        /// </summary>
        [RelayCommand]
        public void CancelInstallation()
        {
            try
            {
                // TODO: Implement proper cancellation token support
                _logger.LogInformation("Installation cancellation requested");
                InstallStatusMessage = "Cancelling installation...";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling installation");
            }
        }

        /// <summary>
        /// Formats a file size in bytes to a human-readable string
        /// </summary>
        /// <param name="bytes">The size in bytes</param>
        /// <returns>A formatted string representation of the file size</returns>
        private string FormatFileSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int suffixIndex = 0;
            double size = bytes;

            while (size >= 1024 && suffixIndex < suffixes.Length - 1)
            {
                size /= 1024;
                suffixIndex++;
            }

            return $"{size:0.##} {suffixes[suffixIndex]}";
        }
        
        /// <summary>
        /// Event fired when an installation operation completes
        /// </summary>
        public event EventHandler<InstallationResult>? InstallationCompleted;

        /// <summary>
        /// Handles changes to the SelectedItem property with enhanced validation
        /// </summary>
        partial void OnSelectedItemChanged(IGitHubDisplayItem? value)
        {
            _logger.LogDebug("SelectedItem changed. New value: {Type}, DisplayName: '{DisplayName}'", 
                value?.GetType().Name, value?.DisplayName);
                
            // Clear previous selection
            SelectedWorkflow = null;
            SelectedArtifact = null;
            SelectedRelease = null;
            
            if (value == null)
            {
                _logger.LogDebug("Selection cleared (null)");
                return;
            }
            
            // Set the appropriate property based on the item type
            switch (value)
            {
                case GitHubWorkflowDisplayItemViewModel workflow:
                    SelectedWorkflow = workflow;
                    _logger.LogDebug("Set SelectedWorkflow: {DisplayName}", workflow.DisplayName);
                    break;
                    
                case GitHubArtifactDisplayItemViewModel artifact:
                    SelectedArtifact = artifact;
                    _logger.LogDebug("Set SelectedArtifact: {DisplayName}, WorkflowNumber: {WorkflowNumber}, CreatedAt: {CreatedAt}", 
                        artifact.DisplayName, artifact.WorkflowNumber, artifact.CreatedAt);
                    
                    // Validate artifact data
                    if (artifact.Artifact == null)
                    {
                        _logger.LogError("SelectedArtifact has null Artifact property!");
                    }
                    else
                    {
                        _logger.LogTrace("Artifact validation - Name: '{Name}', WorkflowNumber: {WorkflowNumber}, BuildInfo: {BuildInfo}", 
                            artifact.Artifact.Name, artifact.Artifact.WorkflowNumber, artifact.Artifact.BuildInfo?.GameVariant);
                    }
                    
                    // Notify UI that artifact-related properties changed
                    OnPropertyChanged(nameof(HasSelectedArtifact));
                    OnPropertyChanged(nameof(HasSelection));
                    break;
                    
                case GitHubReleaseDisplayItemViewModel release:
                    SelectedRelease = release;
                    _logger.LogDebug("Set SelectedRelease: {DisplayName}", release.DisplayName);
                    break;
                    
                default:
                    _logger.LogWarning("Unknown item type: {Type}", value.GetType().Name);
                    break;
            }
        }
    }
}
