using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using CommunityToolkit.Mvvm.Input;

using GenHub.Core.Models;
using GenHub.Core.Models.Results;
using GenHub.Core.Models.GameProfiles;
using GenHub.Core.Interfaces.GitHub;

namespace GenHub.Features.GitHub.ViewModels
{
    /// <summary>
    /// ViewModel for installation operations
    /// </summary>
    public partial class InstallationViewModel : ObservableObject
    {
        private readonly ILogger<InstallationViewModel> _logger;
        private readonly IGitHubArtifactInstaller _artifactInstaller;

        #region Observable Properties
        [ObservableProperty]
        private bool _isInstalling = false;

        [ObservableProperty]
        private double _installProgress = 0;

        [ObservableProperty]
        private string _installStatusMessage = string.Empty;

        [ObservableProperty]
        private GitHubArtifact? _currentArtifact;
        #endregion

        public InstallationViewModel(
            ILogger<InstallationViewModel> logger,
            IGitHubArtifactInstaller artifactInstaller)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _artifactInstaller = artifactInstaller ?? throw new ArgumentNullException(nameof(artifactInstaller));
        }

        /// <summary>
        /// Sets the current artifact for installation
        /// </summary>
        public void SetCurrentArtifact(GitHubArtifact? artifact)
        {
            CurrentArtifact = artifact;
        }

        /// <summary>
        /// Installs the currently selected artifact
        /// </summary>
        [RelayCommand]
        public async Task InstallArtifactAsync()
        {
            if (CurrentArtifact == null || IsInstalling)
                return;

            try
            {
                IsInstalling = true;
                InstallProgress = 0;
                InstallStatusMessage = "Starting installation...";

                // Create progress tracker
                var progress = new Progress<InstallProgress>(p =>
                {
                    InstallProgress = p.Percentage;
                    InstallStatusMessage = p.Message;
                });

                // Install the artifact
                var result = await _artifactInstaller.InstallArtifactAsync(
                    CurrentArtifact,
                    progress);

                if (result.Success && result.Data != null)
                {
                    // Update UI
                    InstallStatusMessage = "Installation successful!";
                    InstallProgress = 1;

                    // Update artifact status
                    CurrentArtifact.IsInstalled = true;
                    
                    // Notify installation success
                    InstallationCompleted?.Invoke(this, new InstallationEventArgs
                    {
                        Success = true,
                        Artifact = CurrentArtifact,
                        GameVersion = result.Data
                    });
                }
                else
                {
                    InstallStatusMessage = result.ErrorMessage ?? "Installation failed";
                    
                    // Notify installation failure
                    InstallationCompleted?.Invoke(this, new InstallationEventArgs
                    {
                        Success = false,
                        Artifact = CurrentArtifact,
                        Error = result.ErrorMessage
                    });
                }
            }
            catch (OperationCanceledException)
            {
                InstallStatusMessage = "Installation cancelled";
                
                // Notify installation cancellation
                InstallationCompleted?.Invoke(this, new InstallationEventArgs
                {
                    Success = false,
                    Artifact = CurrentArtifact,
                    Cancelled = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error installing artifact");
                InstallStatusMessage = $"Error: {ex.Message}";
                
                // Notify installation error
                InstallationCompleted?.Invoke(this, new InstallationEventArgs
                {
                    Success = false,
                    Artifact = CurrentArtifact,
                    Error = ex.Message
                });
            }
            finally
            {
                IsInstalling = false;
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
        /// Event fired when installation completes
        /// </summary>
        public event EventHandler<InstallationEventArgs>? InstallationCompleted;
    }

    /// <summary>
    /// Event arguments for installation completion
    /// </summary>
    public class InstallationEventArgs : EventArgs
    {
        public bool Success { get; set; }
        public GitHubArtifact? Artifact { get; set; }
        public GameVersion? GameVersion { get; set; }
        public bool Cancelled { get; set; }
        public string? Error { get; set; }
    }
}
