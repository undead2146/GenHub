using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Interfaces.AppUpdate;
using GenHub.Core.Models.GitHub;
using Microsoft.Extensions.Logging;

namespace GenHub.Linux.UpdateInstallers
{
    /// <summary>
    /// Linux implementation of the update installer
    /// </summary>
    public class LinuxUpdateInstaller : IUpdateInstaller
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<LinuxUpdateInstaller> _logger;

        public LinuxUpdateInstaller(HttpClient httpClient, ILogger<LinuxUpdateInstaller> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task InstallUpdateAsync(GitHubRelease release, IProgress<UpdateProgress>? progressReporter, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Starting update to version {Version}", release.Version);
                progressReporter?.Report(new UpdateProgress { IsInProgress = true, Status = "Starting update..." });
                
                // Download the update file
                var updateFilePath = Path.Combine(Path.GetTempPath(), $"genhub-update-{release.Version}.tar.gz");
                using (var response = await _httpClient.GetAsync(release.HtmlUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
                {
                    response.EnsureSuccessStatusCode();
                    using (var fileStream = new FileStream(updateFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        var totalBytes = response.Content.Headers.ContentLength ?? 0;
                        long bytesRead = 0; // Changed to long to match ContentLength
                        var buffer = new byte[8192];
                        using (var stream = await response.Content.ReadAsStreamAsync(cancellationToken))
                        {
                            int read;
                            while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                            {
                                await fileStream.WriteAsync(buffer, 0, read, cancellationToken);
                                bytesRead += read;
                                if (totalBytes > 0)
                                {
                                    progressReporter?.Report(new UpdateProgress 
                                    { 
                                        IsInProgress = true, 
                                        Status = "Downloading...", 
                                        PercentageCompleted = (bytesRead / (double)totalBytes) * 100 
                                    });
                                }
                                else
                                {
                                    progressReporter?.Report(new UpdateProgress { IsInProgress = true, Status = "Downloading..." });
                                }
                            }
                        }
                    }
                }

                _logger.LogInformation("Download complete, extracting update at {Path}", updateFilePath);
                progressReporter?.Report(new UpdateProgress { IsInProgress = true, Status = "Extracting...", PercentageCompleted = 50 }); // Adjusted percentage

                // Prepare update directory
                var updateDir = Path.Combine(Path.GetTempPath(), $"genhub-update-{release.Version}");
                if (Directory.Exists(updateDir))
                {
                    Directory.Delete(updateDir, true);
                }
                Directory.CreateDirectory(updateDir);

                // Extract the tar.gz file (Linux approach)
                var extractProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "tar",
                        Arguments = $"xzf \"{updateFilePath}\" -C \"{updateDir}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };
                extractProcess.Start();
                await extractProcess.WaitForExitAsync(cancellationToken);

                if (extractProcess.ExitCode != 0)
                {
                    string error = await extractProcess.StandardError.ReadToEndAsync(cancellationToken);
                    throw new Exception($"Failed to extract update: {error}");
                }

                _logger.LogInformation("Extraction complete, preparing to install");
                progressReporter?.Report(new UpdateProgress { IsInProgress = true, Status = "Installing...", PercentageCompleted = 75 });

                // Find the install script
                var installScript = Path.Combine(updateDir, "install.sh");
                if (!File.Exists(installScript))
                {
                    throw new FileNotFoundException("Installation script not found in update package");
                }

                // Make the script executable
                var chmodProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "chmod",
                        Arguments = $"+x \"{installScript}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                chmodProcess.Start();
                await chmodProcess.WaitForExitAsync(cancellationToken);

                // Run the installation script
                var installProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = installScript,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                installProcess.Start();
                await installProcess.WaitForExitAsync(cancellationToken);

                _logger.LogInformation("Update installation completed successfully");
                progressReporter?.Report(new UpdateProgress { IsInProgress = false, Status = "Update complete", PercentageCompleted = 100, IsSuccessful = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error installing update: {Message}", ex.Message);
                progressReporter?.Report(new UpdateProgress { IsInProgress = false, Status = $"Error: {ex.Message}", Message = ex.ToString(), IsSuccessful = false });
                throw;
            }
            finally
            {
                // Ensure IsInProgress is set to false if not already set by success/error states
                // This might be redundant if all paths set it, but good for safety.
                // Consider if a final "cleanup" state is needed or if success/error states are sufficient.
                // For now, the success/error handlers should set IsInProgress = false.
            }
        }
    }
}
