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
using System.Windows.Input;

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
        private IGitHubDisplayItem? _selectedItem;

        [ObservableProperty]
        private string _selectedArtifactSizeFormatted = string.Empty;

        [ObservableProperty]
        private bool _isInstalling = false;

        [ObservableProperty]
        private double _installProgress = 0;

        [ObservableProperty]
        private string _installStatusMessage = string.Empty;

        [ObservableProperty]
        private GitHubArtifact? _currentlyInstallingArtifact;
        #endregion

        #region Computed Properties for UI Binding
        /// <summary>
        /// Gets a value indicating whether an artifact is selected
        /// </summary>
        public bool HasSelectedArtifact => SelectedItem is GitHubArtifactDisplayItemViewModel;

        /// <summary>
        /// Gets the selected artifact (if any)
        /// </summary>
        public GitHubArtifactDisplayItemViewModel? SelectedArtifact => 
            SelectedItem as GitHubArtifactDisplayItemViewModel;

        /// <summary>
        /// Gets a value indicating whether a workflow is selected
        /// </summary>
        public bool HasSelectedWorkflow => SelectedItem is GitHubWorkflowDisplayItemViewModel;

        /// <summary>
        /// Gets the selected workflow (if any)
        /// </summary>
        public GitHubWorkflowDisplayItemViewModel? SelectedWorkflow => 
            SelectedItem as GitHubWorkflowDisplayItemViewModel;

        /// <summary>
        /// Gets a value indicating whether a release is selected
        /// </summary>
        public bool HasSelectedRelease => SelectedItem is GitHubReleaseDisplayItemViewModel;

        /// <summary>
        /// Gets the selected release (if any)
        /// </summary>
        public GitHubReleaseDisplayItemViewModel? SelectedRelease => 
            SelectedItem as GitHubReleaseDisplayItemViewModel;

        /// <summary>
        /// Gets the workflow run items (artifacts) for the selected workflow
        /// </summary>
        public ObservableCollection<IGitHubDisplayItem> WorkflowRunItems { get; } = new();

        /// <summary>
        /// Gets a value indicating whether there are workflow run items
        /// </summary>
        public bool HasWorkflowRunItems => WorkflowRunItems.Count > 0;

        /// <summary>
        /// Gets a value indicating whether any item is selected
        /// </summary>
        public bool HasSelection => SelectedItem != null;
        
        /// <summary>
        /// Gets the display name safely
        /// </summary>
        public string SafeDisplayName => SelectedItem?.DisplayName ?? string.Empty;

        /// <summary>
        /// Gets the description safely
        /// </summary>
        public string SafeDescription => SelectedItem?.Description ?? string.Empty;
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
        /// Sets the selected item and updates related properties
        /// </summary>
        public void SetSelectedItem(IGitHubDisplayItem? item)
        {
            try
            {
                SelectedItem = item;
                
                // Clear workflow run items
                WorkflowRunItems.Clear();
                
                // Load workflow run items if it's a workflow
                if (item is GitHubWorkflowDisplayItemViewModel workflow)
                {
                    LoadWorkflowRunItems(workflow);
                }
                
                // Notify all property changes for safe binding
                OnPropertyChanged(nameof(HasSelectedArtifact));
                OnPropertyChanged(nameof(SelectedArtifact));
                OnPropertyChanged(nameof(HasSelectedWorkflow));
                OnPropertyChanged(nameof(SelectedWorkflow));
                OnPropertyChanged(nameof(HasSelectedRelease));
                OnPropertyChanged(nameof(SelectedRelease));
                OnPropertyChanged(nameof(HasWorkflowRunItems));
                OnPropertyChanged(nameof(HasSelection));
                OnPropertyChanged(nameof(SafeDisplayName));
                OnPropertyChanged(nameof(SafeDescription));
                
                _logger.LogDebug("Selected item set: {ItemType}", item?.GetType().Name ?? "null");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting selected item");
            }
        }

        /// <summary>
        /// Loads workflow run items (artifacts) for a workflow
        /// </summary>
        private async void LoadWorkflowRunItems(GitHubWorkflowDisplayItemViewModel workflow)
        {
            try
            {
                _logger.LogDebug("Loading workflow run items for: {WorkflowName}", workflow.DisplayName);
                
                // Ensure children are loaded
                if (!workflow.ChildrenLoaded)
                {
                    await workflow.LoadChildrenAsync();
                }
                
                // Add children to workflow run items
                foreach (var child in workflow.Children)
                {
                    WorkflowRunItems.Add(child);
                }
                
                OnPropertyChanged(nameof(HasWorkflowRunItems));
                
                _logger.LogDebug("Loaded {Count} workflow run items", WorkflowRunItems.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading workflow run items");
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
                
            if (value == null)
            {
                _logger.LogDebug("Selection cleared (null)");
                return;
            }
            
            // Validate artifact data if it's an artifact
            if (value is GitHubArtifactDisplayItemViewModel artifact)
            {
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
            }
            
            // Notify UI that selection-related properties changed
            OnPropertyChanged(nameof(HasSelectedArtifact));
            OnPropertyChanged(nameof(HasSelectedWorkflow));
            OnPropertyChanged(nameof(HasSelectedRelease));
            OnPropertyChanged(nameof(HasSelection));
        }

        /// <summary>
        /// Command to select an item from the workflow run items
        /// </summary>
        [RelayCommand]
        private void SelectItem(IGitHubDisplayItem item)
        {
            if (item != null)
            {
                SetSelectedItem(item);
                _logger.LogDebug("Selected item from workflow run items: {DisplayName}", item.DisplayName);
            }
        }
    }
}
