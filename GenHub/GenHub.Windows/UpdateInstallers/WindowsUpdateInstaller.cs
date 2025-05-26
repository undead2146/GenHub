using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using GenHub.Core.Interfaces.AppUpdate;
using GenHub.Core.Models.AppUpdate;
using GenHub.Core.Models;
using GenHub.Core.Models.GitHub;

namespace GenHub.Windows.UpdateInstallers
{
    /// <summary>
    /// Windows-specific implementation of the update installer
    /// </summary>
    public class WindowsUpdateInstaller : IUpdateInstaller
    {
        private readonly ILogger<WindowsUpdateInstaller> _logger;
        private readonly HttpClient _httpClient;
        private readonly string _tempFolder;
        private readonly string _appFolder;
        
        public WindowsUpdateInstaller(
            ILogger<WindowsUpdateInstaller> logger,
            IHttpClientFactory httpClientFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpClient = httpClientFactory?.CreateClient("GitHubApiClient") ?? 
                throw new ArgumentNullException(nameof(httpClientFactory));
                
            _tempFolder = Path.Combine(Path.GetTempPath(), "GenHubUpdates");
            _appFolder = AppContext.BaseDirectory;
            
            // Create temp folder if it doesn't exist
            if (!Directory.Exists(_tempFolder))
            {
                Directory.CreateDirectory(_tempFolder);
            }
            
            _logger.LogInformation("WindowsUpdateInstaller initialized. App folder: {AppFolder}, Temp folder: {TempFolder}", 
                _appFolder, _tempFolder);
        }

        /// <summary>
        /// Gets the supported file extensions for update files on this platform
        /// </summary>
        public string[] SupportedExtensions => new[] { ".zip", ".msi", ".exe" };

        /// <summary>
        /// Installs an application update for Windows
        /// </summary>
        public async Task InstallUpdateAsync(
            GitHubRelease release, 
            IProgress<UpdateProgress>? progress = null, 
            CancellationToken cancellationToken = default)
        {
            if (release == null)
                throw new ArgumentNullException(nameof(release));
                
            if (release.Assets == null || release.Assets.Count == 0)
                throw new InvalidOperationException("No assets available for this release");
                
            _logger.LogInformation("Starting Windows update installation for version {Version}", release.Version);
            
            // Report initial progress
            ReportProgress(progress, "Starting Windows update installation...", 0.0);
            
            try 
            {
                // 1. Find the appropriate asset to download (e.g., a ZIP or MSI file)
                var updateAsset = FindUpdateAsset(release);
                if (updateAsset == null)
                {
                    throw new InvalidOperationException("No suitable update asset found in release");
                }
                
                _logger.LogInformation("Found update asset: {AssetName}, URL: {Url}", 
                    updateAsset.Name, updateAsset.BrowserDownloadUrl);
                
                // 2. Download the asset
                ReportProgress(progress, $"Downloading update: {updateAsset.Name}...", 0.1);
                
                string downloadPath = Path.Combine(_tempFolder, updateAsset.Name);
                
                await DownloadFileAsync(
                    updateAsset.BrowserDownloadUrl, 
                    downloadPath, 
                    progress, 
                    0.1, 0.6, // Progress range from 10% to 60% 
                    cancellationToken);
                
                // 3. Process the downloaded file based on its type
                ReportProgress(progress, "Preparing to install update...", 0.65);
                
                if (updateAsset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    await InstallFromZipAsync(downloadPath, progress, cancellationToken);
                }
                else if (updateAsset.Name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase) || 
                         updateAsset.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    await InstallFromInstallerAsync(downloadPath, progress, cancellationToken);
                }
                else
                {
                    throw new NotSupportedException($"Unsupported update file format: {Path.GetExtension(updateAsset.Name)}");
                }
                
                // 4. Clean up temp files
                ReportProgress(progress, "Cleaning up temporary files...", 0.95);
                try
                {
                    if (File.Exists(downloadPath))
                        File.Delete(downloadPath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to clean up temporary file: {Path}", downloadPath);
                    // Non-critical error, continue
                }
                
                // 5. Report completion
                ReportProgress(progress, "Update installation complete. Restart to apply changes.", 1.0, false);
                _logger.LogInformation("Windows update installation completed for version {Version}", release.Version);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error installing Windows update: {Message}", ex.Message);
                ReportProgress(progress, $"Update failed: {ex.Message}", 0.0, false, false);
                throw;
            }
        }
        
        /// <summary>
        /// Installs an update from the specified file path
        /// </summary>
        public async Task InstallUpdateAsync(string updateFilePath, IProgress<UpdateProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(updateFilePath))
                throw new ArgumentException("Update file path cannot be null or empty", nameof(updateFilePath));

            if (!File.Exists(updateFilePath))
                throw new FileNotFoundException($"Update file not found: {updateFilePath}");

            _logger.LogInformation("Starting Windows update installation from file: {FilePath}", updateFilePath);

            try
            {
                var extension = Path.GetExtension(updateFilePath).ToLowerInvariant();

                if (extension == ".zip")
                {
                    await InstallFromZipAsync(updateFilePath, progress, cancellationToken);
                }
                else if (extension == ".msi" || extension == ".exe")
                {
                    await InstallFromInstallerAsync(updateFilePath, progress, cancellationToken);
                }
                else
                {
                    throw new NotSupportedException($"Unsupported update file format: {extension}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error installing update from file: {FilePath}", updateFilePath);
                throw;
            }
        }

        /// <summary>
        /// Validates that the update file is compatible with the current platform
        /// </summary>
        public bool ValidateUpdateFile(string updateFilePath)
        {
            if (string.IsNullOrEmpty(updateFilePath))
                return false;

            if (!File.Exists(updateFilePath))
                return false;

            var extension = Path.GetExtension(updateFilePath).ToLowerInvariant();
            return SupportedExtensions.Contains(extension);
        }

        /// <summary>
        /// Finds the appropriate asset for the update
        /// </summary>
        private GitHubReleaseAsset? FindUpdateAsset(GitHubRelease release)
        {
            if (release.Assets == null || release.Assets.Count == 0)
                return null;
                
            // Look for Windows-specific assets with the following priority:
            // 1. MSI installer
            // 2. EXE installer
            // 3. ZIP archive
            
            // First try to find a Windows MSI installer
            var asset = release.Assets.Find(a => 
                a.Name.Contains("windows", StringComparison.OrdinalIgnoreCase) && 
                a.Name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase));
                
            if (asset != null)
                return asset;
                
            // Next, try to find a Windows EXE installer
            asset = release.Assets.Find(a => 
                a.Name.Contains("windows", StringComparison.OrdinalIgnoreCase) && 
                a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                a.Name.Contains("setup", StringComparison.OrdinalIgnoreCase));
                
            if (asset != null)
                return asset;
                
            // Next, try to find a Windows ZIP archive
            asset = release.Assets.Find(a => 
                a.Name.Contains("windows", StringComparison.OrdinalIgnoreCase) && 
                a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
                
            if (asset != null)
                return asset;
                
            // Generic fallbacks if no Windows-specific assets are found
            
            // MSI installer
            asset = release.Assets.Find(a => a.Name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase));
            if (asset != null)
                return asset;
                
            // EXE installer
            asset = release.Assets.Find(a => 
                a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && 
                !a.Name.Contains("portable", StringComparison.OrdinalIgnoreCase));
                
            if (asset != null)
                return asset;
                
            // ZIP archive
            asset = release.Assets.Find(a => a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
            if (asset != null)
                return asset;
                
            // No suitable asset found
            return null;
        }
        
        /// <summary>
        /// Downloads a file with progress reporting
        /// </summary>
        private async Task DownloadFileAsync(
            string url, 
            string destinationPath, 
            IProgress<UpdateProgress>? progress,
            double progressStart, 
            double progressEnd,
            CancellationToken cancellationToken)
        {
            try
            {
                // Create a temporary file for download
                string tempFile = destinationPath + ".download";
                
                // Remove existing temp file if it exists
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
                    
                // Create parent directory if it doesn't exist
                var directoryPath = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }
                
                using (var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
                {
                    response.EnsureSuccessStatusCode();
                    
                    var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                    
                    using (var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken))
                    using (var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                    {
                        var buffer = new byte[8192];
                        var bytesRead = 0;
                        var totalBytesRead = 0L;
                        
                        while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
                        {
                            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                            
                            totalBytesRead += bytesRead;
                            
                            if (totalBytes > 0)
                            {
                                double downloadProgress = (double)totalBytesRead / totalBytes;
                                double overallProgress = progressStart + (progressEnd - progressStart) * downloadProgress;
                                
                                string message = $"Downloaded {GetFileSizeString(totalBytesRead)} of {GetFileSizeString(totalBytes)}";
                                ReportProgress(progress, "Downloading update...", overallProgress, true, true, message);
                            }
                        }
                    }
                }
                
                // Replace the destination file with the downloaded file
                if (File.Exists(destinationPath))
                    File.Delete(destinationPath);
                    
                File.Move(tempFile, destinationPath);
                
                _logger.LogInformation("Download complete: {Path}", destinationPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading file from {Url}: {Message}", url, ex.Message);
                throw;
            }
        }
        
        /// <summary>
        /// Installs the update from a ZIP archive
        /// </summary>
        private async Task InstallFromZipAsync(string zipPath, IProgress<UpdateProgress>? progress, CancellationToken cancellationToken)
        {
            ReportProgress(progress, "Extracting update files...", 0.7);
            
            try
            {
                // Extract to a temporary directory
                string extractPath = Path.Combine(_tempFolder, "Extract_" + Guid.NewGuid().ToString("N"));
                
                // Use System.IO.Compression to extract the ZIP
                _logger.LogInformation("Extracting ZIP: {ZipPath} to {ExtractPath}", zipPath, extractPath);
                
                // Create extract directory
                Directory.CreateDirectory(extractPath);
                
                // Extract the ZIP file
                await Task.Run(() => System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, extractPath), cancellationToken);
                
                ReportProgress(progress, "Installing update files...", 0.8);
                
                // Create a batch script that will:
                // 1. Wait for the application to exit
                // 2. Copy all files from the extract directory to the app directory
                // 3. Restart the application
                
                string batchFile = Path.Combine(_tempFolder, "update.bat");
                string appExe = Process.GetCurrentProcess().MainModule?.FileName ?? 
                    Path.Combine(_appFolder, "GenHub.exe");
                
                await File.WriteAllTextAsync(batchFile, @$"
@echo off
echo Waiting for GenHub to close...
ping -n 3 127.0.0.1 > nul
echo Installing update...
xcopy ""{extractPath}\*.*"" ""{_appFolder}"" /E /Y /I
echo Starting GenHub...
start """" ""{appExe}""
echo Cleaning up...
rmdir /S /Q ""{extractPath}""
del ""{zipPath}""
del ""%~f0""
", cancellationToken);

                // Launch the batch script
                ReportProgress(progress, "Finalizing update...", 0.9);
                
                var processInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {batchFile}",
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                
                Process.Start(processInfo);
                
                // The application should exit now to allow the batch script to complete the update
                ReportProgress(progress, "Update ready. Application will now close to complete the installation.", 1.0, false);
                _logger.LogInformation("Update is ready to install. Application will now close.");
                
                // Wait a moment to ensure the progress message is shown
                await Task.Delay(1500, cancellationToken);
                
                // Exit the application
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error installing from ZIP: {Message}", ex.Message);
                throw;
            }
        }
        
        /// <summary>
        /// Installs the update from an installer file (MSI or EXE)
        /// </summary>
        private async Task InstallFromInstallerAsync(string installerPath, IProgress<UpdateProgress>? progress, CancellationToken cancellationToken)
        {
            ReportProgress(progress, "Running installer...", 0.7);
            
            try
            {
                var extension = Path.GetExtension(installerPath).ToLowerInvariant();
                
                ProcessStartInfo startInfo;
                
                if (extension == ".msi")
                {
                    startInfo = new ProcessStartInfo
                    {
                        FileName = "msiexec.exe",
                        Arguments = $"/i \"{installerPath}\" /qb",
                        UseShellExecute = true
                    };
                }
                else // .exe
                {
                    startInfo = new ProcessStartInfo
                    {
                        FileName = installerPath,
                        UseShellExecute = true
                    };
                }
                
                _logger.LogInformation("Launching installer: {Path}", installerPath);
                var process = Process.Start(startInfo);
                
                if (process != null)
                {
                    ReportProgress(progress, "Installer is running. Follow the on-screen instructions to complete installation.", 0.9, true);
                    
                    // Wait for installer to exit or timeout after 5 minutes
                    try
                    {
                        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
                        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
                        
                        await Task.Run(() => process.WaitForExit(), linkedCts.Token);
                        
                        if (process.ExitCode == 0)
                        {
                            ReportProgress(progress, "Installation completed successfully. Please restart the application.", 1.0, false);
                        }
                        else
                        {
                            ReportProgress(progress, $"Installer exited with code {process.ExitCode}. Installation may not have completed successfully.", 1.0, false, false);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogWarning("Installer timeout or cancellation. Installation may still be in progress.");
                        ReportProgress(progress, "Installation is taking longer than expected. The installer may still be running.", 0.95, true);
                    }
                }
                else
                {
                    throw new InvalidOperationException("Failed to start the installer process");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running installer: {Message}", ex.Message);
                throw;
            }
        }
        
        /// <summary>
        /// Reports progress with standardized format
        /// </summary>
        private void ReportProgress(
            IProgress<UpdateProgress>? reporter, 
            string status, 
            double progress,
            bool isInProgress = true,
            bool isSuccessful = true,
            string? message = null)
        {
            if (reporter == null)
                return;
                
            reporter.Report(new UpdateProgress
            {
                Status = status,
                PercentageCompleted = progress,
                IsInProgress = isInProgress,
                IsSuccessful = isSuccessful,
                Message = message
            });
        }
        
        /// <summary>
        /// Formats a file size in bytes to a human-readable string
        /// </summary>
        private string GetFileSizeString(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            
            return $"{len:0.##} {sizes[order]}";
        }
    }
}
