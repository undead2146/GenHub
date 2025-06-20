using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using GenHub.Core.Interfaces.GitHub;

namespace GenHub.Features.GitHub.ViewModels
{
    /// <summary>
    /// ViewModel for managing the installation of GitHub artifacts and releases
    /// </summary>
    public partial class InstallationViewModel : ObservableObject, IDisposable
    {
        private readonly ILogger<InstallationViewModel> _logger;
        private CancellationTokenSource? _installationCts;

        #region Observable Properties
        [ObservableProperty]
        private IGitHubDisplayItem? _selectedItem;

        [ObservableProperty]
        private bool _isInstalling = false;

        [ObservableProperty]
        private double _progress = 0.0;

        [ObservableProperty]
        private bool _canInstall = false;

        [ObservableProperty]
        private string _statusMessage = "Ready";
        #endregion

        #region Events
        /// <summary>
        /// Event fired when installation is completed
        /// </summary>
        public event EventHandler<InstallationCompletedEventArgs>? InstallationCompleted;
        #endregion

        public InstallationViewModel(ILogger<InstallationViewModel> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Sets the selected item for installation
        /// </summary>
        public void SetSelectedItem(IGitHubDisplayItem? item)
        {
            SelectedItem = item;
            UpdateCanInstall();
            
            if (item != null)
            {
                StatusMessage = $"Ready to install {item.DisplayName}";
            }
            else
            {
                StatusMessage = "No item selected";
            }
        }

        /// <summary>
        /// Updates the CanInstall property based on current state
        /// </summary>
        private void UpdateCanInstall()
        {
            CanInstall = SelectedItem != null && !IsInstalling && SelectedItem.CanInstall;
        }

        /// <summary>
        /// Installs the selected item using its actual install command
        /// </summary>
        [RelayCommand]
        private async Task InstallAsync()
        {
            if (SelectedItem?.InstallCommand == null || IsInstalling)
                return;

            _installationCts = new CancellationTokenSource();

            try
            {
                IsInstalling = true;
                Progress = 0;
                StatusMessage = "Starting installation...";

                _logger.LogInformation("Starting installation for: {ItemName}", SelectedItem.DisplayName);

                // Check if command can execute
                if (!SelectedItem.InstallCommand.CanExecute(null))
                {
                    throw new InvalidOperationException("Item cannot be installed at this time");
                }

                // Execute the actual install command
                if (SelectedItem.InstallCommand is IAsyncRelayCommand asyncCommand)
                {
                    await asyncCommand.ExecuteAsync(null);
                }
                else
                {
                    SelectedItem.InstallCommand.Execute(null);
                }

                // Monitor progress if the item supports it
                await MonitorInstallationProgress();

                StatusMessage = "Installation completed successfully";
                Progress = 100;
                
                _logger.LogInformation("Installation completed for: {ItemName}", SelectedItem.DisplayName);

                InstallationCompleted?.Invoke(this, new InstallationCompletedEventArgs
                {
                    Success = true,
                    InstalledItem = SelectedItem
                });
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "Installation cancelled";
                _logger.LogInformation("Installation cancelled by user");
            }
            catch (Exception ex)
            {
                StatusMessage = $"Installation failed: {ex.Message}";
                _logger.LogError(ex, "Error during installation");

                InstallationCompleted?.Invoke(this, new InstallationCompletedEventArgs
                {
                    Success = false,
                    Message = ex.Message,
                    InstalledItem = SelectedItem
                });
            }
            finally
            {
                IsInstalling = false;
                Progress = 0;
                UpdateCanInstall();
                _installationCts?.Dispose();
                _installationCts = null;
            }
        }

        /// <summary>
        /// Monitors installation progress by checking the selected item's state
        /// </summary>
        private async Task MonitorInstallationProgress()
        {
            // Since we can't directly monitor the installation progress from the command,
            // we'll simulate progress monitoring by checking the item's state
            var startTime = DateTime.Now;
            const int maxWaitSeconds = 300; // 5 minutes max

            while (!_installationCts.Token.IsCancellationRequested && 
                   (DateTime.Now - startTime).TotalSeconds < maxWaitSeconds)
            {
                // Check if item has progress properties we can monitor
                if (SelectedItem is GitHubArtifactDisplayItemViewModel artifactViewModel)
                {
                    if (artifactViewModel.IsInstalling)
                    {
                        StatusMessage = "Installing artifact...";
                        Progress = Math.Min(Progress + 2, 90); // Gradually increase progress
                    }
                    else if (!artifactViewModel.IsInstalling && Progress > 0)
                    {
                        // Installation finished
                        break;
                    }
                }
                else
                {
                    // For other types, just simulate progress
                    Progress = Math.Min(Progress + 3, 90);
                    StatusMessage = $"Installing... {Progress:F0}%";
                }

                await Task.Delay(500, _installationCts.Token);
            }
        }

        /// <summary>
        /// Cancels the current installation
        /// </summary>
        [RelayCommand]
        private void CancelInstallation()
        {
            if (IsInstalling && _installationCts != null)
            {
                _installationCts.Cancel();
                _logger.LogInformation("Installation cancellation requested by user");
            }
        }

        /// <summary>
        /// Property change handler for IsInstalling
        /// </summary>
        partial void OnIsInstallingChanged(bool value)
        {
            UpdateCanInstall();
        }

        /// <summary>
        /// Property change handler for SelectedItem
        /// </summary>
        partial void OnSelectedItemChanged(IGitHubDisplayItem? value)
        {
            UpdateCanInstall();
        }

        /// <summary>
        /// Dispose resources when ViewModel is disposed
        /// </summary>
        public void Dispose()
        {
            _installationCts?.Cancel();
            _installationCts?.Dispose();
        }
    }

    /// <summary>
    /// Event arguments for installation completion
    /// </summary>
    public class InstallationCompletedEventArgs : EventArgs
    {
        public bool Success { get; set; }
        public IGitHubDisplayItem? InstalledItem { get; set; }
        public string? Message { get; set; }
    }
}
