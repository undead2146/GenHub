using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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
            
            // Set icon based on asset type
            UpdateIconKey();
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
        public bool CanDownload => !IsDownloading && !string.IsNullOrEmpty(_asset.BrowserDownloadUrl);
        
        /// <summary>
        /// Downloads the asset
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanDownload))]
        private async Task DownloadAsync()
        {
            if (IsDownloading) return;
            
            IsDownloading = true;
            DownloadProgress = 0;
            
            try
            {
                _logger.LogInformation("Starting download for asset: {AssetName} ({AssetId})", Name, Id);
                
                _downloadCts = new CancellationTokenSource();
                
                // Create destination folder
                string downloadsPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), 
                    "Downloads");
                
                string destinationPath = Path.Combine(downloadsPath, Name);
                
                // Check if file already exists and add a number if needed
                string baseName = Path.GetFileNameWithoutExtension(Name);
                string extension = Path.GetExtension(Name);
                int counter = 1;
                
                while (File.Exists(destinationPath))
                {
                    destinationPath = Path.Combine(
                        downloadsPath, 
                        $"{baseName}_{counter}{extension}");
                    counter++;
                }
                
                // Download progress reporter
                var progress = new Progress<double>(value => DownloadProgress = value * 100);
                
                // Download the file
                var (stream, contentLength) = await _gitHubService.DownloadReleaseAssetAsync(
                    BrowserDownloadUrl, 
                    _downloadCts.Token);
                
                if (stream == Stream.Null)
                {
                    throw new InvalidOperationException("Failed to get download stream");
                }
                
                // Save the stream to file with progress tracking
                using (var fileStream = File.Create(destinationPath))
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
                _logger.LogInformation("Download completed: {FilePath}", destinationPath);
                
                // Open containing folder
                OpenContainingFolder(destinationPath);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Download cancelled for asset: {AssetName}", Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading asset: {AssetName} ({AssetId})", Name, Id);
            }
            finally
            {
                IsDownloading = false;
                _downloadCts?.Dispose();
                _downloadCts = null;
            }
        }
        
        /// <summary>
        /// Cancels the download
        /// </summary>
        [RelayCommand]
        private void CancelDownload()
        {
            if (!IsDownloading || _downloadCts == null) return;
            
            _downloadCts.Cancel();
            _logger.LogInformation("Download cancelled for asset: {AssetName}", Name);
        }
        
        /// <summary>
        /// Updates the icon key based on file type
        /// </summary>
        private void UpdateIconKey()
        {
            string extension = Path.GetExtension(Name).ToLowerInvariant();
            
            switch (extension)
            {
                case ".zip":
                case ".7z":
                case ".rar":
                case ".tar":
                case ".gz":
                    _iconKey = "ArchiveIcon";
                    break;
                case ".exe":
                case ".msi":
                case ".dmg":
                    _iconKey = "ExecutableIcon";
                    break;
                case ".iso":
                    _iconKey = "DiskIcon";
                    break;
                case ".txt":
                case ".md":
                case ".log":
                    _iconKey = "DocumentIcon";
                    break;
                default:
                    _iconKey = "FileIcon";
                    break;
            }
        }
        
        /// <summary>
        /// Loads children for this item - assets have no children
        /// </summary>
        public override Task LoadChildrenAsync(CancellationToken cancellationToken = default)
        {
            // Assets don't have children, so this is a no-op
            return Task.CompletedTask;
        }
        
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
    }
}
