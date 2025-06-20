using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using GenHub.Core.Models.GitHub;
using GenHub.Core.Interfaces;
using GenHub.Core.Interfaces.GitHub;

namespace GenHub.Features.GitHub.ViewModels
{
    /// <summary>
    /// View model for a GitHub release asset
    /// </summary>
    public partial class GitHubReleaseAssetViewModel : GitHubDisplayItemViewModel
    {
        private readonly GitHubReleaseAsset _asset;
        private readonly GitHubReleaseDisplayItemViewModel _parentRelease;
        private readonly IGitHubServiceFacade _gitHubService;
        private readonly ILogger _logger;
        private CancellationTokenSource? _downloadCts;
        
        // Override the abstract DisplayName property
        public override string DisplayName => _asset.Name;
        
        [ObservableProperty]
        private bool _isDownloading;
        
        [ObservableProperty]
        private double _downloadProgress;
        
        /// <summary>
        /// Initializes a new instance of the GitHubReleaseAssetViewModel class
        /// </summary>
        public GitHubReleaseAssetViewModel(
            GitHubReleaseAsset asset,
            GitHubReleaseDisplayItemViewModel parentRelease,
            IGitHubServiceFacade gitHubService,
            ILogger logger)
        {
            _asset = asset ?? throw new ArgumentNullException(nameof(asset));
            _parentRelease = parentRelease ?? throw new ArgumentNullException(nameof(parentRelease));
            _gitHubService = gitHubService ?? throw new ArgumentNullException(nameof(gitHubService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _iconKey = "AssetIcon";

            _logger.LogDebug("Created GitHubReleaseAssetViewModel for asset {AssetName} (ID: {AssetId})",
                asset.Name, asset.Id);
        }
        
        /// <summary>
        /// Gets the asset ID
        /// </summary>
        public long Id => _asset.Id;
        
        /// <summary>
        /// Gets the asset name
        /// </summary>
        public string Name => _asset.Name;
        
        /// <summary>
        /// Gets the content type
        /// </summary>
        public string ContentType => _asset.ContentType;
        
        /// <summary>
        /// Gets the size in bytes
        /// </summary>
        public long Size => _asset.Size;
        
        /// <summary>
        /// Gets the download URL
        /// </summary>
        public string BrowserDownloadUrl => _asset.BrowserDownloadUrl;
        
        /// <summary>
        /// Gets the sort date (created at date)
        /// </summary>
        public override DateTime SortDate => _asset.CreatedAt;
        
        /// <summary>
        /// Gets a value indicating whether this item is expandable (it's not)
        /// </summary>
        public override bool IsExpandable => false;

        /// <summary>
        /// Gets the description (file information)
        /// </summary>
        public override string Description => $"{FormatFileSize(_asset.Size)} - {_asset.ContentType}";

        public override bool IsRelease => true;
        
        /// <summary>
        /// Gets a value indicating whether this asset can be downloaded
        /// </summary>
        public override bool CanDownload => !IsDownloading && !string.IsNullOrEmpty(_asset.BrowserDownloadUrl);

        /// <summary>
        /// Downloads the release asset using proper GitHubServiceFacade
        /// </summary>
        [RelayCommand]
        private async Task DownloadAssetAsync()
        {
            if (!CanDownload)
                return;

            IsDownloading = true;
            _downloadCts = new CancellationTokenSource();

            try
            {
                _logger.LogInformation("Starting download of asset: {AssetName}", _asset.Name);

                var downloadFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "GenHub", "Downloads");
                Directory.CreateDirectory(downloadFolder);

                // Create progress tracker
                var progress = new Progress<double>(value => 
                {
                    DownloadProgress = value * 100;
                    _logger.LogDebug("Download progress for {AssetName}: {Progress}%", _asset.Name, DownloadProgress);
                });

                // Use proper GitHubServiceFacade method
                var (stream, contentLength) = await _gitHubService.DownloadReleaseAssetAsync(
                    _asset.BrowserDownloadUrl, 
                    _downloadCts.Token);

                if (stream == Stream.Null)
                {
                    throw new InvalidOperationException("Failed to get download stream");
                }

                // Determine file path
                var fileName = Path.GetFileName(_asset.Name);
                var filePath = Path.Combine(downloadFolder, fileName);
                
                // Check if file already exists and add number if needed
                var baseName = Path.GetFileNameWithoutExtension(fileName);
                var extension = Path.GetExtension(fileName);
                int counter = 1;
                
                while (File.Exists(filePath))
                {
                    filePath = Path.Combine(downloadFolder, $"{baseName}_{counter}{extension}");
                    counter++;
                }

                // Save the stream to file with progress tracking
                using (var fileStream = File.Create(filePath))
                {
                    long bytesRead = 0;
                    byte[] buffer = new byte[81920]; // 80 KB buffer
                    int read;
                    
                    while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, _downloadCts.Token)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, read, _downloadCts.Token);
                        bytesRead += read;
                        
                        // Report progress if we know the content length
                        if (contentLength.HasValue && contentLength.Value > 0)
                        {
                            double progressValue = (double)bytesRead / contentLength.Value;
                            ((IProgress<double>)progress).Report(progressValue);
                        }
                    }
                }

                DownloadProgress = 100;
                _logger.LogInformation("Successfully downloaded asset to: {FilePath}", filePath);
                OpenContainingFolder(filePath);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Download cancelled for asset: {AssetName}", _asset.Name);
                DownloadProgress = 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading asset: {AssetName}", _asset.Name);
                DownloadProgress = 0;
            }
            finally
            {
                IsDownloading = false;
                _downloadCts?.Dispose();
                _downloadCts = null;
            }
        }

        /// <summary>
        /// Gets the download command
        /// </summary>
        public override ICommand? DownloadCommand => DownloadAssetCommand;

        /// <summary>
        /// Loads children for this item - assets have no children
        /// </summary>
        public override Task LoadChildrenAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
        
        /// <summary>
        /// Cancels the download
        /// </summary>
        public void CancelDownload()
        {
            _downloadCts?.Cancel();
        }

        /// <summary>
        /// Opens the containing folder of the downloaded file
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
                _logger.LogWarning(ex, "Could not open containing folder for {FilePath}", filePath);
            }
        }

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
    }
}
