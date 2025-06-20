using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Interfaces.DesktopShortcuts;
using GenHub.Core.Models.AdvancedLauncher;
using GenHub.Core.Models.Results;
using Microsoft.Extensions.Logging;

namespace GenHub.Linux.DesktopShortcuts
{
    /// <summary>
    /// Linux implementation of desktop shortcut service using .desktop files
    /// </summary>
    public class LinuxShortcutService : IShortcutPlatformService
    {
        private readonly ILogger<LinuxShortcutService> _logger;
        private readonly string _desktopPath;
        private readonly string _applicationsPath;

        public LinuxShortcutService(ILogger<LinuxShortcutService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _desktopPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
            _applicationsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "applications");
        }

        /// <summary>
        /// Creates a desktop shortcut using .desktop file format
        /// </summary>
        /// <param name="config">Shortcut configuration</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Creation result</returns>
        public async Task<OperationResult> CreateShortcutAsync(ShortcutConfiguration config, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Creating Linux desktop shortcut: {Name}", config.Name);

                // Validate configuration
                var validationResult = ValidateConfiguration(config);
                if (!validationResult.IsSuccess)
                {
                    return validationResult;
                }

                // Create .desktop file content
                var desktopContent = CreateDesktopFileContent(config);
                
                // Determine file paths
                var fileName = SanitizeFileName(config.Name) + ".desktop";
                var desktopFilePath = Path.Combine(_desktopPath, fileName);
                var applicationsFilePath = Path.Combine(_applicationsPath, fileName);

                // Ensure directories exist
                Directory.CreateDirectory(Path.GetDirectoryName(desktopFilePath)!);
                Directory.CreateDirectory(Path.GetDirectoryName(applicationsFilePath)!);

                // Write desktop file to desktop
                await File.WriteAllTextAsync(desktopFilePath, desktopContent, cancellationToken);
                
                // Write desktop file to applications menu
                await File.WriteAllTextAsync(applicationsFilePath, desktopContent, cancellationToken);

                // Make files executable
                SetExecutablePermissions(desktopFilePath);
                SetExecutablePermissions(applicationsFilePath);                _logger.LogInformation("Successfully created Linux desktop shortcut: {FilePath}", desktopFilePath);
                return OperationResult.Succeeded();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create Linux desktop shortcut: {Name}", config.Name);
                return OperationResult.Failed($"Failed to create shortcut: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates if a shortcut exists
        /// </summary>
        /// <param name="shortcutName">Name of the shortcut</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Validation result</returns>
        public async Task<OperationResult<bool>> ValidateShortcutAsync(string shortcutName, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Validating Linux desktop shortcut: {Name}", shortcutName);

                var fileName = SanitizeFileName(shortcutName) + ".desktop";
                var desktopFilePath = Path.Combine(_desktopPath, fileName);
                var applicationsFilePath = Path.Combine(_applicationsPath, fileName);

                var desktopExists = File.Exists(desktopFilePath);
                var applicationsExists = File.Exists(applicationsFilePath);

                // Shortcut is valid if it exists in either location
                var exists = desktopExists || applicationsExists;

                return OperationResult<bool>.Success(exists);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to validate Linux desktop shortcut: {Name}", shortcutName);
                return OperationResult<bool>.Failed($"Failed to validate shortcut: {ex.Message}");
            }
        }

        /// <summary>
        /// Removes a desktop shortcut
        /// </summary>
        /// <param name="shortcutName">Name of the shortcut</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Removal result</returns>
        public async Task<OperationResult> RemoveShortcutAsync(string shortcutName, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Removing Linux desktop shortcut: {Name}", shortcutName);

                var fileName = SanitizeFileName(shortcutName) + ".desktop";
                var desktopFilePath = Path.Combine(_desktopPath, fileName);
                var applicationsFilePath = Path.Combine(_applicationsPath, fileName);

                var removed = false;

                // Remove from desktop
                if (File.Exists(desktopFilePath))
                {
                    File.Delete(desktopFilePath);
                    removed = true;
                    _logger.LogDebug("Removed desktop shortcut: {FilePath}", desktopFilePath);
                }

                // Remove from applications menu
                if (File.Exists(applicationsFilePath))
                {
                    File.Delete(applicationsFilePath);
                    removed = true;
                    _logger.LogDebug("Removed applications shortcut: {FilePath}", applicationsFilePath);
                }

                if (removed)
                {                    _logger.LogInformation("Successfully removed Linux desktop shortcut: {Name}", shortcutName);
                    return OperationResult.Succeeded();
                }
                else
                {
                    _logger.LogWarning("Linux desktop shortcut not found: {Name}", shortcutName);
                    return OperationResult.Failed($"Shortcut not found: {shortcutName}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove Linux desktop shortcut: {Name}", shortcutName);
                return OperationResult.Failed($"Failed to remove shortcut: {ex.Message}");
            }
        }

        /// <summary>
        /// Repairs a desktop shortcut by recreating it
        /// </summary>
        /// <param name="config">Shortcut configuration</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Repair result</returns>
        public async Task<OperationResult> RepairShortcutAsync(ShortcutConfiguration config, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Repairing Linux desktop shortcut: {Name}", config.Name);

                // Remove existing shortcut
                await RemoveShortcutAsync(config.Name, cancellationToken);

                // Create new shortcut
                return await CreateShortcutAsync(config, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to repair Linux desktop shortcut: {Name}", config.Name);
                return OperationResult.Failed($"Failed to repair shortcut: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets all desktop shortcuts
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of shortcuts</returns>
        public async Task<OperationResult<IEnumerable<string>>> GetAllShortcutsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Getting all Linux desktop shortcuts");

                var shortcuts = new List<string>();

                // Get shortcuts from desktop
                if (Directory.Exists(_desktopPath))
                {
                    var desktopFiles = Directory.GetFiles(_desktopPath, "*.desktop")
                        .Select(f => Path.GetFileNameWithoutExtension(f));
                    shortcuts.AddRange(desktopFiles);
                }

                // Get shortcuts from applications menu
                if (Directory.Exists(_applicationsPath))
                {
                    var applicationFiles = Directory.GetFiles(_applicationsPath, "*.desktop")
                        .Select(f => Path.GetFileNameWithoutExtension(f));
                    shortcuts.AddRange(applicationFiles);
                }

                // Remove duplicates
                var uniqueShortcuts = shortcuts.Distinct().ToList();

                _logger.LogDebug("Found {Count} Linux desktop shortcuts", uniqueShortcuts.Count);
                return OperationResult<IEnumerable<string>>.Success(uniqueShortcuts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get Linux desktop shortcuts");
                return OperationResult<IEnumerable<string>>.Failed($"Failed to get shortcuts: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates shortcut configuration
        /// </summary>
        /// <param name="config">Configuration to validate</param>
        /// <returns>Validation result</returns>
        private OperationResult ValidateConfiguration(ShortcutConfiguration config)
        {
            if (string.IsNullOrWhiteSpace(config.Name))
            {
                return OperationResult.Failed("Shortcut name is required");
            }

            if (string.IsNullOrWhiteSpace(config.ExecutablePath))
            {
                return OperationResult.Failed("Executable path is required");
            }

            if (!File.Exists(config.ExecutablePath))
            {
                return OperationResult.Failed($"Executable not found: {config.ExecutablePath}");
            }

            return OperationResult.Succeeded();
        }

        /// <summary>
        /// Creates .desktop file content
        /// </summary>
        /// <param name="config">Shortcut configuration</param>
        /// <returns>Desktop file content</returns>
        private string CreateDesktopFileContent(ShortcutConfiguration config)
        {
            var content = new List<string>
            {
                "[Desktop Entry]",
                "Version=1.0",
                "Type=Application",
                $"Name={config.Name}",
                $"Exec={config.ExecutablePath}"
            };

            if (!string.IsNullOrWhiteSpace(config.Arguments))
            {
                content[content.Count - 1] += $" {config.Arguments}";
            }

            if (!string.IsNullOrWhiteSpace(config.WorkingDirectory))
            {
                content.Add($"Path={config.WorkingDirectory}");
            }

            if (!string.IsNullOrWhiteSpace(config.Description))
            {
                content.Add($"Comment={config.Description}");
            }

            if (!string.IsNullOrWhiteSpace(config.IconPath) && File.Exists(config.IconPath))
            {
                content.Add($"Icon={config.IconPath}");
            }

            content.Add("Terminal=false");
            content.Add("StartupNotify=true");
            content.Add("Categories=Game;");

            return string.Join("\n", content) + "\n";
        }

        /// <summary>
        /// Sanitizes a filename for use in file system
        /// </summary>
        /// <param name="filename">Original filename</param>
        /// <returns>Sanitized filename</returns>
        private string SanitizeFileName(string filename)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = new string(filename.Where(c => !invalidChars.Contains(c)).ToArray());
            return string.IsNullOrWhiteSpace(sanitized) ? "GenHub-Shortcut" : sanitized;
        }

        /// <summary>
        /// Sets executable permissions on a file (Linux-specific)
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        private void SetExecutablePermissions(string filePath)
        {
            try
            {
                // Use chmod to set executable permissions
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "chmod",
                        Arguments = $"+x \"{filePath}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                process.WaitForExit();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to set executable permissions on {FilePath}", filePath);
            }
        }
    }
}
