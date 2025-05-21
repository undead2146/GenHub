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
        private GitHubArtifact? _selectedArtifact;

        [ObservableProperty]
        private string _selectedArtifactSizeFormatted = string.Empty;

        [ObservableProperty]
        private GitHubWorkflowDisplayItemViewModel? _selectedWorkflowGroup;

        [ObservableProperty]
        private GitHubReleaseDisplayItemViewModel? _selectedRelease;

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
                    SelectedArtifact = artifactViewModel.Artifact;
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
            if (SelectedArtifact == null || IsInstalling)
                return;

            try
            {
                IsInstalling = true;
                InstallProgress = 0;
                InstallStatusMessage = "Starting installation...";
                CurrentlyInstallingArtifact = SelectedArtifact;

                // Create progress tracker
                var progress = new Progress<InstallProgress>(p =>
                {
                    InstallProgress = p.Percentage;
                    InstallStatusMessage = p.Message;
                });

                // Install the artifact
                var gameVersion = await _gitHubService.InstallArtifactAsync(
                    SelectedArtifact,
                    progress);

                if (gameVersion != null)
                {
                    // Update UI
                    InstallStatusMessage = "Installation successful!";
                    InstallProgress = 1;

                    // Update artifact status
                    SelectedArtifact.IsInstalled = true;
                }
                else
                {
                    InstallStatusMessage = "Installation failed";
                }
                
                // Notify installation result
                InstallationCompleted?.Invoke(this, new InstallationResult 
                { 
                    Success = gameVersion != null,
                    Artifact = SelectedArtifact,
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
