using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using GenHub.Core.Models.GitHub;
using GenHub.Core.Models;
using GenHub.Core.Interfaces.GitHub;
using GenHub.Core.Interfaces;

namespace GenHub.Features.GitHub.ViewModels
{
    /// <summary>
    /// View model for a GitHub workflow artifact
    /// </summary>
    public partial class GitHubArtifactDisplayItemViewModel : GitHubDisplayItemViewModel
    {
        private readonly ILogger _logger;
        private readonly IGitHubServiceFacade? _gitHubService;
        private CancellationTokenSource? _downloadCts;
        private string _displayName = string.Empty;
        
        // Artifact backing data
        public GitHubArtifact Artifact { get; }
        
        // Observable properties for UI binding
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
        private int _runNumber;
        
        [ObservableProperty]
        private bool _canBeInstalled;

        [ObservableProperty]
        private long _workflowId;

        [ObservableProperty]
        private long _workflowRunId;
        
        /// <summary>
        /// Initializes a new instance of the GitHubArtifactDisplayItemViewModel class
        /// </summary>
        public GitHubArtifactDisplayItemViewModel(
            GitHubArtifact artifact,
            IGitHubServiceFacade? gitHubService,
            ILogger logger)
        {
            Artifact = artifact ?? throw new ArgumentNullException(nameof(artifact));
            _gitHubService = gitHubService;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Initialize properties
            Name = artifact.Name ?? string.Empty;
            SizeFormatted = FormatFileSize(artifact.SizeInBytes);
            RunNumber = artifact.WorkflowNumber;
            WorkflowId = artifact.WorkflowId;
            WorkflowRunId = artifact.RunId;
            CanBeInstalled = artifact.BuildInfo != null && !string.IsNullOrEmpty(artifact.Name);
            _displayName = artifact.Name ?? string.Empty;
            IsInstalled = artifact.IsInstalled;
            
            // Set the icon based on the artifact type
            SetIconKey();
        }
        
        /// <summary>
        /// Gets the size in bytes of the artifact
        /// </summary>
        public long SizeInBytes => Artifact.SizeInBytes;
        
        // Override base properties
        public override string DisplayName => _displayName;
        public override string Description => $"{SizeFormatted} - Run #{RunNumber}";
        public override bool IsExpandable => false;
        public override DateTime SortDate => Artifact.CreatedAt;
        public override bool IsRelease => false;
        
        /// <summary>
        /// Sets the appropriate icon key based on the artifact
        /// </summary>
        private void SetIconKey()
        {
            if (Artifact.BuildInfo != null)
            {
                // Set icon based on build info
                switch (Artifact.BuildInfo.GameVariant)
                {
                    case GameVariant.Generals:
                        _iconKey = "GeneralsIcon";
                        break;
                    case GameVariant.ZeroHour:
                        _iconKey = "ZeroHourIcon";
                        break;
                    default:
                        _iconKey = "PackageIcon";
                        break;
                }
            }
            else
            {
                // Generic icon based on name
                string extension = Path.GetExtension(Name).ToLowerInvariant();
                
                switch (extension)
                {
                    case ".zip":
                    case ".7z":
                        _iconKey = "ArchiveIcon";
                        break;
                    case ".exe":
                        _iconKey = "ExecutableIcon";
                        break;
                    default:
                        _iconKey = "PackageIcon";
                        break;
                }
            }
        }
        
        /// <summary>
        /// Downloads the artifact
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanDownload))]
        private async Task DownloadAsync()
        {
            if (IsDownloading || _gitHubService == null)
                return;
            
            IsDownloading = true;
            DownloadProgress = 0;
            
            try
            {
                _logger.LogInformation("Starting download for artifact: {ArtifactName} ({ArtifactId})", 
                    Name, Artifact.Id);
                
                _downloadCts = new CancellationTokenSource();
                
                // Create destination folder
                string downloadsPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Downloads");
                
                Directory.CreateDirectory(downloadsPath);
                
                // Show progress
                var progress = new Progress<double>(value => DownloadProgress = value * 100);
                
                // Download the file
                string downloadedFile = await _gitHubService.DownloadArtifactAsync(
                    Artifact.Id,
                    downloadsPath,
                    progress,
                    _downloadCts.Token);
                
                if (string.IsNullOrEmpty(downloadedFile))
                {
                    throw new Exception("Download failed - no file returned");
                }
                
                DownloadProgress = 100;
                _logger.LogInformation("Download completed: {FilePath}", downloadedFile);
                
                // Open containing folder
                OpenContainingFolder(downloadedFile);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Download cancelled for artifact: {ArtifactName}", Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading artifact: {ArtifactName} ({ArtifactId})", Name, Artifact.Id);
            }
            finally
            {
                IsDownloading = false;
                _downloadCts?.Dispose();
                _downloadCts = null;
            }
        }
        
        /// <summary>
        /// Installs the artifact
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanInstall))]
        private async Task InstallAsync()
        {
            if (IsInstalling || IsDownloading || _gitHubService == null)
                return;
            
            IsInstalling = true;
            
            try
            {
                _logger.LogInformation("Starting installation for artifact: {ArtifactName}", Name);
                
                // Report progress
                var progress = new Progress<Core.Models.Results.InstallProgress>(p =>
                {
                    DownloadProgress = p.Percentage;
                });
                
                // Install through the service
                var gameVersion = await _gitHubService.InstallArtifactAsync(
                    Artifact,
                    progress);
                
                if (gameVersion != null)
                {
                    IsInstalled = true;
                    _logger.LogInformation("Installation completed for artifact: {ArtifactName}", Name);
                }
                else
                {
                    _logger.LogWarning("Installation failed for artifact: {ArtifactName}", Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error installing artifact: {ArtifactName}", Name);
            }
            finally
            {
                IsInstalling = false;
            }
        }
        
        /// <summary>
        /// Cancels the download
        /// </summary>
        [RelayCommand(CanExecute = nameof(IsDownloading))]
        private void CancelDownload()
        {
            _downloadCts?.Cancel();
            _logger.LogInformation("Download cancelled for artifact: {ArtifactName}", Name);
        }
        
        /// <summary>
        /// Checks if the download can be started
        /// </summary>
        public bool CanDownload => !IsDownloading && !IsInstalling && _gitHubService != null;
        
        /// <summary>
        /// Checks if the installation can be started
        /// </summary>
        public bool CanInstall => !IsInstalling && !IsDownloading && CanBeInstalled && !IsInstalled && _gitHubService != null;
        
        /// <summary>
        /// Formats a file size in bytes to human-readable format
        /// </summary>
        private string FormatFileSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            decimal size = bytes;
            
            while (size >= 1024 && counter < suffixes.Length - 1)
            {
                size /= 1024;
                counter++;
            }
            
            return $"{size:0.##} {suffixes[counter]}";
        }
        
        /// <summary>
        /// Opens the folder containing a file
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
                else
                {
                    _logger.LogWarning("Opening folder not supported on this platform");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error opening containing folder for {FilePath}", filePath);
            }
        }
    }
}
