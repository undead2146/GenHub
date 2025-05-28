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
    /// ViewModel for GitHub item details display
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
        public bool HasSelection => SelectedGitHubItem != null || SelectedArtifact != null;

        public bool HasSelectedArtifact => SelectedArtifact != null;

        public bool HasSelectedRelease => SelectedGitHubItem is GitHubReleaseDisplayItemViewModel;

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

                // Handle artifact display items
                if (item is GitHubArtifactDisplayItemViewModel artifactViewModel)
                {
                    // Assign the ViewModel
                    SelectedArtifact = artifactViewModel;
                    SelectedArtifactSizeFormatted = FormatFileSize(artifactViewModel.SizeInBytes);
                }
                // Handle workflow display items
                else if (item is GitHubWorkflowDisplayItemViewModel workflow)
                {
                    SelectedWorkflowGroup = workflow;

                    // First load the artifacts
                    _ = workflow.LoadChildrenAsync();

                    // After loading, update the workflow run items with the artifacts
                    _ = UpdateWorkflowRunItemsFromWorkflowAsync(workflow);
                }
                // Handle release display items
                else if (item is GitHubReleaseDisplayItemViewModel release)
                {
                    SelectedRelease = release;

                    // First load the assets
                    _ = release.LoadChildrenAsync();

                    // Update the workflow run items with the assets
                    _ = UpdateWorkflowRunItemsFromReleaseAsync(release);
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
        /// Updates items from a workflow
        /// </summary>
        private async Task UpdateWorkflowRunItemsFromWorkflowAsync(GitHubWorkflowDisplayItemViewModel workflow)
        {
            try
            {
                // Wait for async loading to complete
                await Task.Delay(100);
                
                // Update collection
                WorkflowRunItems.Clear();
                foreach (var item in workflow.Artifacts)
                {
                    WorkflowRunItems.Add(item);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating workflow run items");
            }
        }

        /// <summary>
        /// Updates items from a release
        /// </summary>
        private async Task UpdateWorkflowRunItemsFromReleaseAsync(GitHubReleaseDisplayItemViewModel release)
        {
            try
            {
                // Wait for async loading to complete
                await Task.Delay(100);
                
                // Update collection
                WorkflowRunItems.Clear();
                foreach (var asset in release.Assets)
                {
                    WorkflowRunItems.Add(asset);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating release assets");
            }
        }

        /// <summary>
        /// Installs the selected artifact
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

                // Create progress tracker
                var progress = new Progress<InstallProgress>(p =>
                {
                    InstallProgress = p.Percentage;
                    InstallStatusMessage = p.Message;
                });

                // Install the artifact - use the actual GitHubArtifact model
                var gameVersion = await _gitHubService.InstallArtifactAsync(
                    artifactToInstall,
                    progress);

                if (gameVersion != null)
                {
                    // Update UI
                    InstallStatusMessage = "Installation successful!";
                    InstallProgress = 1;

                    // Update artifact status
                    artifactToInstall.IsInstalled = true;
                    // Add null check
                    if (SelectedArtifact != null)
                    {
                        SelectedArtifact.IsInstalled = true; 
                    }
                }
                else
                {
                    InstallStatusMessage = "Installation failed";
                }
                
                // Notify installation result
                InstallationCompleted?.Invoke(this, new InstallationResult 
                { 
                    Success = gameVersion != null,
                    Artifact = artifactToInstall,
                    GameVersion = gameVersion
                });
            }
            catch (OperationCanceledException)
            {
                InstallStatusMessage = "Installation cancelled";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error installing artifact");
                InstallStatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsInstalling = false;
                CurrentlyInstallingArtifact = null;
            }
        }

        /// <summary>
        /// Cancels the current installation
        /// </summary>
        [RelayCommand]
        public void CancelInstallation()
        {
            try
            {
                // TODO: Implement cancellation
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
        /// Event fired when an installation completes
        /// </summary>
        public event EventHandler<InstallationResult>? InstallationCompleted;

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
                    
                    // Validate artifact data immediately after assignment
                    if (artifact.Artifact == null)
                    {
                        _logger.LogError("SelectedArtifact has null Artifact property!");
                    }
                    else
                    {
                        _logger.LogDebug("Artifact validation - Name: '{Name}', WorkflowNumber: {WorkflowNumber}, BuildInfo: {BuildInfo}", 
                            artifact.Artifact.Name, artifact.Artifact.WorkflowNumber, artifact.Artifact.BuildInfo?.GameVariant);
                        
                        // Test property access immediately
                        var testName = artifact.Name;
                        var testBuildInfo = artifact.BuildInfo;
                        var testWorkflowNumber = artifact.WorkflowNumber;
                        
                        _logger.LogDebug("Property access test - Name: '{Name}', BuildInfo: {BuildInfo}, WorkflowNumber: {WorkflowNumber}", 
                            testName, testBuildInfo?.GameVariant, testWorkflowNumber);
                    }
                    
                    // Notify UI that SelectedArtifact properties changed
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

    /// <summary>
    /// Result of an installation operation
    /// </summary>
    public class InstallationResult
    {
        public bool Success { get; set; }
        public GitHubArtifact? Artifact { get; set; }
        public GameVersion? GameVersion { get; set; }
    }
}
