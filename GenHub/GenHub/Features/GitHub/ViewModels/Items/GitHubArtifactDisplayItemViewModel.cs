using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using GenHub.Core.Models.GitHub;
using GenHub.Core.Models;
using GenHub.Core.Models.Results;
using GenHub.Core.Interfaces.GitHub;
using GenHub.Core.Interfaces;

namespace GenHub.Features.GitHub.ViewModels
{
    /// <summary>
    /// View model for a GitHub workflow artifact with installation and download capabilities
    /// </summary>
    public partial class GitHubArtifactDisplayItemViewModel : GitHubDisplayItemViewModel
    {
        private readonly ILogger _logger;
        private readonly IGitHubServiceFacade? _gitHubService;
        private CancellationTokenSource? _downloadCts;
        
        // Manually created commands to avoid conflicts
        private IRelayCommand? _downloadArtifactCommand;
        private IRelayCommand? _installArtifactCommand;
        
        public GitHubArtifact Artifact { get; }
        
        #region Observable Properties
        [ObservableProperty]
        private bool _isDownloading;
        
        [ObservableProperty]
        private double _downloadProgress;
        
        [ObservableProperty]
        private bool _isInstalled;
        
        [ObservableProperty]
        private bool _isInstalling;
        
        [ObservableProperty]
        private string _name = string.Empty;
        
        [ObservableProperty]
        private string _sizeFormatted = string.Empty;

        [ObservableProperty]
        private bool _canBeInstalled;

        [ObservableProperty]
        private long _workflowId;

        [ObservableProperty]
        private long _workflowRunId;
        #endregion
        
        
        public long SizeInBytes => Artifact.SizeInBytes;
        
        #region Overridden Properties
        public override string DisplayName => Artifact.Name ?? string.Empty;
        
        public override string Description => $"{SizeFormatted} - Run #{WorkflowNumber}";
        
        public override bool IsExpandable => false;
        public override DateTime SortDate => Artifact.CreatedAt;
        public override bool IsRelease => false;
        #endregion
        
        public GitHubArtifactDisplayItemViewModel(
            GitHubArtifact artifact,
            IGitHubServiceFacade? gitHubService,
            ILogger logger)
        {
            Artifact = artifact ?? throw new ArgumentNullException(nameof(artifact));
            _gitHubService = gitHubService;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            InitializeFromArtifact();
            SetIconKey();
            InitializeCommands();

            _logger.LogDebug("Created GitHubArtifactDisplayItemViewModel for artifact {ArtifactName} (ID: {ArtifactId})",
                artifact.Name, artifact.Id);
        }

        private void InitializeFromArtifact()
        {
            Name = Artifact.Name ?? string.Empty;
            SizeFormatted = FormatFileSize(Artifact.SizeInBytes);
            WorkflowId = Artifact.WorkflowId;
            WorkflowRunId = Artifact.RunId;
            CanBeInstalled = Artifact.BuildInfo != null && !string.IsNullOrEmpty(Artifact.Name);
            IsInstalled = Artifact.IsInstalled;
        }

        private void InitializeCommands()
        {
            _downloadArtifactCommand = new AsyncRelayCommand(DownloadAsync, () => CanExecuteDownload);
            _installArtifactCommand = new AsyncRelayCommand(InstallAsync, () => CanExecuteInstall);
        }
        
        private void SetIconKey()
        {
            if (Artifact.BuildInfo != null)
            {
                _iconKey = "ArtifactIcon";
            }
            else
            {
                _iconKey = "FileIcon";
            }
        }
        
        #region Commands

        // Use different method names to avoid conflicts with interface properties
        private bool CanExecuteDownload => !IsDownloading && !IsInstalling && _gitHubService != null;
        private bool CanExecuteInstall => !IsInstalling && !IsDownloading && CanBeInstalled && !IsInstalled && _gitHubService != null;

        /// <summary>
        /// Downloads the artifact to the local machine using proper GitHubServiceFacade
        /// </summary>
        private async Task DownloadAsync()
        {
            if (!CanExecuteDownload || _gitHubService == null)
                return;

            _downloadCts = new CancellationTokenSource();

            try
            {
                IsDownloading = true;
                DownloadProgress = 0;

                _logger.LogInformation("Starting download for artifact: {ArtifactName} (ID: {ArtifactId})", 
                    Name, Artifact.Id);

                // Create progress tracker that updates the UI
                var progress = new Progress<double>(value => 
                {
                    DownloadProgress = value * 100; // Convert to percentage
                    _logger.LogDebug("Download progress for {ArtifactName}: {Progress}%", Name, DownloadProgress);
                });

                // Use the proper GitHubServiceFacade method
                var downloadsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "GenHub", "Downloads");
                Directory.CreateDirectory(downloadsFolder);

                var filePath = await _gitHubService.DownloadArtifactAsync(
                    Artifact.Id,
                    downloadsFolder,
                    progress,
                    _downloadCts.Token);

                // Handle the result properly
                if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                {
                    DownloadProgress = 100;
                    _logger.LogInformation("Download completed for artifact: {ArtifactName} to {FilePath}", Name, filePath);
                    
                    // Open the download location
                    try
                    {
                        OpenContainingFolder(filePath);
                        _logger.LogDebug("Opened download folder for {FilePath}", filePath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not open download folder for {FilePath}", filePath);
                    }
                }
                else
                {
                    _logger.LogWarning("Download failed for artifact: {ArtifactName}. No valid file path returned", Name);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Download cancelled for artifact: {ArtifactName}", Name);
                DownloadProgress = 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading artifact: {ArtifactName} (ID: {ArtifactId})", 
                    Name, Artifact.Id);
                DownloadProgress = 0;
            }
            finally
            {
                IsDownloading = false;
                _downloadCts?.Dispose();
                _downloadCts = null;
                
                // Update command can execute states
                _downloadArtifactCommand?.NotifyCanExecuteChanged();
                _installArtifactCommand?.NotifyCanExecuteChanged();
            }
        }

        /// <summary>
        /// Installs the artifact as a game version using proper GitHubServiceFacade
        /// </summary>
        private async Task InstallAsync()
        {
            if (!CanExecuteInstall || _gitHubService == null)
                return;

            IsInstalling = true;
            
            try
            {
                _logger.LogInformation("Starting installation for artifact: {ArtifactName} (ID: {ArtifactId})", 
                    Name, Artifact.Id);

                var progress = new Progress<InstallProgress>(p =>
                {
                    _logger.LogDebug("Installation progress for {ArtifactName}: {Percentage}% - {Message}", 
                        Name, p.Percentage * 100, p.Message);
                });

                // Use the proper GitHubServiceFacade method
                var gameVersion = await _gitHubService.InstallArtifactAsync(
                    Artifact,
                    progress);

                // Handle the result properly
                if (gameVersion != null && !string.IsNullOrEmpty(gameVersion.Id))
                {
                    IsInstalled = true;
                    _logger.LogInformation("Installation completed for artifact: {ArtifactName}. Game version: {GameVersionName} (ID: {GameVersionId})", 
                        Name, gameVersion.DisplayName ?? gameVersion.Name, gameVersion.Id);
                }
                else
                {
                    _logger.LogWarning("Installation failed for artifact: {ArtifactName}. No valid game version returned", Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error installing artifact: {ArtifactName} (ID: {ArtifactId})", 
                    Name, Artifact.Id);
            }
            finally
            {
                IsInstalling = false;
                
                // Update command can execute states
                _downloadArtifactCommand?.NotifyCanExecuteChanged();
                _installArtifactCommand?.NotifyCanExecuteChanged();
            }
        }

        /// <summary>
        /// Opens the folder containing the downloaded file
        /// </summary>
        private void OpenContainingFolder(string filePath)
        {
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    string argument = $"/select,\"{filePath}\"";
                    System.Diagnostics.Process.Start("explorer.exe", argument);
                }
                else if (OperatingSystem.IsLinux())
                {
                    string directory = Path.GetDirectoryName(filePath) ?? "/";
                    System.Diagnostics.Process.Start("xdg-open", directory);
                }
                else if (OperatingSystem.IsMacOS())
                {
                    string directory = Path.GetDirectoryName(filePath) ?? "/";
                    System.Diagnostics.Process.Start("open", directory);
                }
                else
                {
                    _logger.LogWarning("Opening folder not supported on this platform");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error opening containing folder for file: {FilePath}", filePath);
            }
        }
        #endregion
        
        #region Utility Methods
        /// <summary>
        /// Formats a file size in bytes to human-readable format
        /// </summary>
        private static string FormatFileSize(long bytes)
        {
            if (bytes < 1024)
                return $"{bytes} B";
            if (bytes < 1024 * 1024)
                return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024)
                return $"{bytes / (1024.0 * 1024.0):F1} MB";
            
            return $"{bytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
        }

        /// <summary>
        /// Gets the workflow number for display
        /// </summary>
        public int WorkflowNumber => Artifact?.WorkflowNumber ?? 0;
        
        /// <summary>
        /// Gets the creation date
        /// </summary>
        public DateTime CreatedAt => Artifact?.CreatedAt ?? DateTime.MinValue;
        
        /// <summary>
        /// Gets the build information
        /// </summary>
        public GitHubBuild? BuildInfo => Artifact?.BuildInfo;
        
        /// <summary>
        /// Gets the artifact name
        /// </summary>
        public string ArtifactName => Artifact?.Name ?? string.Empty;
        
        /// <summary>
        /// Gets the repository information
        /// </summary>
        public GitHubRepository? RepositoryInfo => Artifact?.RepositoryInfo;
        #endregion
        
        #region Interface Property Overrides
        /// <summary>
        /// Gets the run number for this artifact
        /// </summary>
        public override int? RunNumber => WorkflowNumber > 0 ? WorkflowNumber : null;
        
        /// <summary>
        /// Gets a value indicating whether this artifact can be downloaded
        /// </summary>
        public override bool CanDownload => CanExecuteDownload;
        
        /// <summary>
        /// Gets a value indicating whether this artifact can be installed
        /// </summary>
        public override bool CanInstall => CanExecuteInstall;

        /// <summary>
        /// Gets the download command
        /// </summary>
        public override ICommand? DownloadCommand => _downloadArtifactCommand;

        /// <summary>
        /// Gets the install command
        /// </summary>
        public override ICommand? InstallCommand => _installArtifactCommand;

        /// <summary>
        /// Loads children for this item - artifacts have no children
        /// </summary>
        public override Task LoadChildrenAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Property change handlers to update command states
        /// </summary>
        partial void OnIsDownloadingChanged(bool value) => UpdateCommandStates();
        partial void OnIsInstallingChanged(bool value) => UpdateCommandStates();
        partial void OnIsInstalledChanged(bool value) => UpdateCommandStates();
        
        private void UpdateCommandStates()
        {
            _downloadArtifactCommand?.NotifyCanExecuteChanged();
            _installArtifactCommand?.NotifyCanExecuteChanged();
        }
        #endregion
    }
}
