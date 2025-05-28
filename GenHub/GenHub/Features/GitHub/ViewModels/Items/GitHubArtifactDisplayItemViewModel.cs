using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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
        private int _runNumber;
        
        [ObservableProperty]
        private bool _canBeInstalled;

        [ObservableProperty]
        private long _workflowId;

        [ObservableProperty]
        private long _workflowRunId;
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
        }

        private void InitializeFromArtifact()
        {
            Name = Artifact.Name ?? string.Empty;
            SizeFormatted = FormatFileSize(Artifact.SizeInBytes);
            RunNumber = Math.Max(Artifact.WorkflowNumber, 0);
            WorkflowId = Artifact.WorkflowId;
            WorkflowRunId = Artifact.RunId;
            CanBeInstalled = Artifact.BuildInfo != null && !string.IsNullOrEmpty(Artifact.Name);
            IsInstalled = Artifact.IsInstalled;
        }
        
        public long SizeInBytes => Artifact.SizeInBytes;
        
        #region Overridden Properties
        public override string DisplayName => Artifact.Name ?? string.Empty;
        
        public override string Description => $"{SizeFormatted} - Run #{RunNumber}";
        
        public override bool IsExpandable => false;
        public override DateTime SortDate => Artifact.CreatedAt;
        public override bool IsRelease => false;
        #endregion
        
        private void SetIconKey()
        {
            if (Artifact.BuildInfo != null)
            {
                // Set icon based on game variant
                _iconKey = Artifact.BuildInfo.GameVariant switch
                {
                    GameVariant.Generals => "GeneralsIcon",
                    GameVariant.ZeroHour => "ZeroHourIcon",
                    _ => "FileIcon"
                };
            }
            else
            {
                // Set icon based on file extension
                string extension = Path.GetExtension(Artifact.Name).ToLowerInvariant();
                _iconKey = extension switch
                {
                    ".zip" or ".7z" => "ArchiveIcon",
                    ".exe" => "ExecutableIcon",
                    _ => "FileIcon"
                };
            }
        }
        
        #region Commands
        /// <summary>
        /// Downloads the artifact to the local machine
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanDownload))]
        private async Task DownloadAsync()
        {
            if (IsDownloading || _gitHubService == null)
                return;

            _downloadCts = new CancellationTokenSource();
            IsDownloading = true;
            DownloadProgress = 0;

            try
            {
                _logger.LogInformation("Starting download of artifact: {ArtifactName}", Artifact.Name);

                var downloadFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "GenHub", "Downloads");
                Directory.CreateDirectory(downloadFolder);

                var progress = new Progress<double>(p => DownloadProgress = p);

                var downloadedFile = await _gitHubService.DownloadArtifactAsync(
                    Artifact.Id,
                    downloadFolder,
                    progress,
                    _downloadCts.Token);

                _logger.LogInformation("Successfully downloaded artifact to: {FilePath}", downloadedFile);
                OpenContainingFolder(downloadedFile);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Download cancelled for artifact: {ArtifactName}", Artifact.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading artifact: {ArtifactName}", Artifact.Name);
            }
            finally
            {
                IsDownloading = false;
                DownloadProgress = 0;
                _downloadCts?.Dispose();
                _downloadCts = null;
            }
        }
        
        /// <summary>
        /// Installs the artifact as a game version
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanInstall))]
        private async Task InstallAsync()
        {
            if (IsInstalling || IsDownloading || _gitHubService == null)
                return;

            IsInstalling = true;

            try
            {
                _logger.LogInformation("Starting installation of artifact: {ArtifactName}", Artifact.Name);

                var progress = new Progress<InstallProgress>(p =>
                {
                    // Progress updates could be handled by parent view model
                    _logger.LogTrace("Installation progress: {Percentage}% - {Message}", p.Percentage * 100, p.Message);
                });

                var gameVersion = await _gitHubService.InstallArtifactAsync(Artifact, progress);

                if (gameVersion != null)
                {
                    IsInstalled = true;
                    _logger.LogInformation("Successfully installed artifact: {ArtifactName} as {VersionName}",
                        Artifact.Name, gameVersion.Name);
                }
                else
                {
                    _logger.LogWarning("Installation completed but no game version was returned for: {ArtifactName}",
                        Artifact.Name);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Installation cancelled for artifact: {ArtifactName}", Artifact.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error installing artifact: {ArtifactName}", Artifact.Name);
            }
            finally
            {
                IsInstalling = false;
            }
        }
        
        /// <summary>
        /// Cancels the current download operation
        /// </summary>
        [RelayCommand(CanExecute = nameof(IsDownloading))]
        private void CancelDownload()
        {
            _downloadCts?.Cancel();
            _logger.LogInformation("Download cancellation requested for artifact: {ArtifactName}", Artifact.Name);
        }
        #endregion
        
        #region Command CanExecute Properties
        /// <summary>
        /// Gets a value indicating whether the artifact can be downloaded
        /// </summary>
        public bool CanDownload => !IsDownloading && !IsInstalling && _gitHubService != null;
        
        /// <summary>
        /// Gets a value indicating whether the artifact can be installed
        /// </summary>
        public bool CanInstall => !IsInstalling && !IsDownloading && CanBeInstalled && !IsInstalled && _gitHubService != null;
        #endregion
        
        #region Utility Methods
        /// <summary>
        /// Formats a file size in bytes to human-readable format
        /// </summary>
        /// <param name="bytes">The size in bytes</param>
        /// <returns>A formatted string representation of the file size</returns>
        private string FormatFileSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            decimal size = bytes;
            
            while (Math.Round(size / 1024) >= 1 && counter < suffixes.Length - 1)
            {
                size /= 1024;
                counter++;
            }
            
            return $"{size:n1} {suffixes[counter]}";
        }
        
        /// <summary>
        /// Opens the folder containing the specified file
        /// </summary>
        /// <param name="filePath">The path to the file</param>
        private void OpenContainingFolder(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    var folder = Path.GetDirectoryName(filePath);
                    if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = folder,
                            UseShellExecute = true
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error opening containing folder for {FilePath}", filePath);
            }
        }
        #endregion
        
        #region Simplified Property Accessors
        /// <summary>
        /// Gets the creation date of the artifact
        /// </summary>
        public DateTime CreatedAt => Artifact?.CreatedAt ?? DateTime.MinValue;
        
        /// <summary>
        /// Gets the workflow number associated with this artifact
        /// </summary>
        public int WorkflowNumber => Artifact?.WorkflowNumber ?? 0;

        /// <summary>
        /// Gets the build information for this artifact
        /// </summary>
        public GitHubBuild? BuildInfo => Artifact?.BuildInfo;

        /// <summary>
        /// Gets the artifact name
        /// </summary>
        public string ArtifactName => Artifact?.Name ?? string.Empty;

        /// <summary>
        /// Gets the repository information for this artifact
        /// </summary>
        public GitHubRepoSettings? RepositoryInfo => Artifact?.RepositoryInfo;
        #endregion
    }
}
