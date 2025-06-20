using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Interfaces.DesktopShortcuts;
using GenHub.Core.Models.AdvancedLauncher;
using GenHub.Core.Models.Results;
using Microsoft.Extensions.Logging;

namespace GenHub.Windows.Services.DesktopShortcuts
{
    /// <summary>
    /// Windows implementation of desktop shortcut service using .lnk files and Shell COM interface
    /// </summary>
    public class WindowsShortcutService : IShortcutPlatformService
    {
        private readonly ILogger<WindowsShortcutService> _logger;
        private readonly IShortcutCommandBuilder _commandBuilder;
        private readonly string _desktopPath;
        private readonly string _genHubExecutablePath;

        /// <summary>
        /// Initializes a new instance of the WindowsShortcutService
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="commandBuilder">Command builder for creating launch arguments</param>
        public WindowsShortcutService(ILogger<WindowsShortcutService> logger, IShortcutCommandBuilder commandBuilder)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _commandBuilder = commandBuilder ?? throw new ArgumentNullException(nameof(commandBuilder));
            _desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            _genHubExecutablePath = Assembly.GetExecutingAssembly().Location;
            
            // If we get a .dll path, try to find the .exe
            if (_genHubExecutablePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                var exePath = Path.ChangeExtension(_genHubExecutablePath, ".exe");
                if (File.Exists(exePath))
                {
                    _genHubExecutablePath = exePath;
                }
            }
            
            _logger.LogDebug("WindowsShortcutService initialized - Desktop: {DesktopPath}, GenHub: {GenHubPath}", 
                _desktopPath, _genHubExecutablePath);
        }

        /// <inheritdoc />
        public async Task<OperationResult> CreateShortcutAsync(ShortcutConfiguration configuration, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Creating Windows shortcut for {Name}", configuration.Name);

                if (configuration == null)
                {
                    return OperationResult.Failed("Shortcut configuration cannot be null");
                }

                if (string.IsNullOrWhiteSpace(configuration.Name))
                {
                    return OperationResult.Failed("Shortcut name cannot be empty");
                }

                if (!File.Exists(_genHubExecutablePath))
                {
                    return OperationResult.Failed($"GenHub executable not found: {_genHubExecutablePath}");
                }

                var shortcutPath = GetShortcutPath(configuration);
                var arguments = _commandBuilder.BuildCommandLine(configuration);
                
                // Create shortcut using Shell COM interface
                var result = await CreateLinkFileAsync(configuration, shortcutPath, arguments, cancellationToken);
                
                if (result.Success)
                {
                    _logger.LogInformation("Windows shortcut created successfully: {ShortcutPath}", shortcutPath);
                }
                else
                {
                    _logger.LogError("Failed to create Windows shortcut: {Error}", result.Message);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating Windows shortcut for {Name}", configuration?.Name);
                return OperationResult.Failed($"Unexpected error creating shortcut: {ex.Message}");
            }
        }

        /// <inheritdoc />
        public async Task<OperationResult> RemoveShortcutAsync(ShortcutConfiguration configuration, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Removing Windows shortcut for {Name}", configuration.Name);

                var shortcutPath = GetShortcutPath(configuration);
                
                if (!File.Exists(shortcutPath))
                {
                    _logger.LogWarning("Shortcut file not found: {ShortcutPath}", shortcutPath);
                    return OperationResult.Succeeded("Shortcut does not exist");
                }

                await Task.Run(() => File.Delete(shortcutPath), cancellationToken);
                
                _logger.LogInformation("Windows shortcut removed successfully: {ShortcutPath}", shortcutPath);
                return OperationResult.Succeeded($"Shortcut removed: {shortcutPath}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing Windows shortcut for {Name}", configuration?.Name);
                return OperationResult.Failed($"Error removing shortcut: {ex.Message}");
            }
        }

        /// <inheritdoc />
        public async Task<OperationResult<ShortcutValidationResult>> ValidateShortcutAsync(ShortcutConfiguration configuration, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Validating Windows shortcut for {Name}", configuration.Name);

                var shortcutPath = GetShortcutPath(configuration);
                
                if (!File.Exists(shortcutPath))
                {
                    var failureResult = ShortcutValidationResult.Failure(configuration, "Shortcut file does not exist");
                    return OperationResult<ShortcutValidationResult>.Succeeded(failureResult);
                }

                // Validate shortcut target using Shell COM interface
                var validationResult = await ValidateLinkFileAsync(configuration, shortcutPath, cancellationToken);
                
                _logger.LogDebug("Windows shortcut validation completed for {Name}: {IsValid}", 
                    configuration.Name, validationResult.IsValid);

                return OperationResult<ShortcutValidationResult>.Succeeded(validationResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating Windows shortcut for {Name}", configuration?.Name);
                if (configuration != null)
                {
                    var errorResult = ShortcutValidationResult.Failure(configuration, $"Validation error: {ex.Message}");
                    return OperationResult<ShortcutValidationResult>.Succeeded(errorResult);
                }
                return OperationResult<ShortcutValidationResult>.Failed($"Validation error: {ex.Message}");
            }
        }

        /// <inheritdoc />
        public async Task<OperationResult> RepairShortcutAsync(ShortcutConfiguration configuration, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Repairing Windows shortcut for {Name}", configuration.Name);

                var shortcutPath = GetShortcutPath(configuration);
                
                if (!File.Exists(shortcutPath))
                {
                    return OperationResult.Failed("Cannot repair shortcut: file does not exist");
                }

                // Repair by recreating the shortcut
                var arguments = _commandBuilder.BuildCommandLine(configuration);
                var result = await CreateLinkFileAsync(configuration, shortcutPath, arguments, cancellationToken);
                
                if (result.Success)
                {
                    _logger.LogInformation("Windows shortcut repaired successfully: {ShortcutPath}", shortcutPath);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error repairing Windows shortcut for {Name}", configuration?.Name);
                return OperationResult.Failed($"Error repairing shortcut: {ex.Message}");
            }
        }

        /// <inheritdoc />
        public string GetShortcutPath(ShortcutConfiguration configuration)
        {
            if (configuration == null || string.IsNullOrWhiteSpace(configuration.Name))
            {
                throw new ArgumentException("Configuration and name cannot be null or empty", nameof(configuration));
            }

            var sanitizedName = SanitizeFileName(configuration.Name);
            return Path.Combine(_desktopPath, $"{sanitizedName}.lnk");
        }

        /// <inheritdoc />
        public bool SupportsShortcutType(ShortcutType shortcutType)
        {
            return shortcutType == ShortcutType.Profile || shortcutType == ShortcutType.Game;
        }

        /// <inheritdoc />
        public string[] GetSupportedExtensions()
        {
            return new[] { ".lnk" };
        }

        /// <summary>
        /// Creates a .lnk file using Shell COM interface
        /// </summary>
        private async Task<OperationResult> CreateLinkFileAsync(ShortcutConfiguration configuration, string shortcutPath, string arguments, CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // Use IWshRuntimeLibrary (Windows Script Host) for .lnk creation
                    Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                    if (shellType == null)
                    {
                        return OperationResult.Failed("Windows Script Host not available");
                    }

                    dynamic shell = Activator.CreateInstance(shellType)!;
                    dynamic shortcut = shell.CreateShortcut(shortcutPath);

                    shortcut.TargetPath = _genHubExecutablePath;
                    
                    if (!string.IsNullOrWhiteSpace(arguments))
                    {
                        shortcut.Arguments = arguments;
                    }

                    if (!string.IsNullOrWhiteSpace(configuration.WorkingDirectory))
                    {
                        shortcut.WorkingDirectory = configuration.WorkingDirectory;
                    }
                    else
                    {
                        shortcut.WorkingDirectory = Path.GetDirectoryName(_genHubExecutablePath);
                    }

                    if (!string.IsNullOrWhiteSpace(configuration.Description))
                    {
                        shortcut.Description = configuration.Description;
                    }

                    if (!string.IsNullOrWhiteSpace(configuration.IconPath))
                    {
                        shortcut.IconLocation = $"{configuration.IconPath},0";
                    }

                    shortcut.Save();

                    // Release COM objects
                    Marshal.ReleaseComObject(shortcut);
                    Marshal.ReleaseComObject(shell);

                    return OperationResult.Succeeded($"Shortcut created: {shortcutPath}");
                }
                catch (Exception ex)
                {
                    return OperationResult.Failed($"COM error creating shortcut: {ex.Message}");
                }
            }, cancellationToken);
        }

        /// <summary>
        /// Validates a .lnk file using Shell COM interface
        /// </summary>
        private async Task<ShortcutValidationResult> ValidateLinkFileAsync(ShortcutConfiguration configuration, string shortcutPath, CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                try
                {
                    Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                    if (shellType == null)
                    {
                        return ShortcutValidationResult.Failure(configuration, "Windows Script Host not available");
                    }

                    dynamic shell = Activator.CreateInstance(shellType)!;
                    dynamic shortcut = shell.CreateShortcut(shortcutPath);

                    string targetPath = shortcut.TargetPath;
                    
                    // Release COM objects
                    Marshal.ReleaseComObject(shortcut);
                    Marshal.ReleaseComObject(shell);

                    // Validate target exists and matches expected GenHub executable
                    if (string.IsNullOrWhiteSpace(targetPath))
                    {
                        return ShortcutValidationResult.Failure(configuration, "Shortcut has no target path");
                    }

                    if (!File.Exists(targetPath))
                    {
                        return ShortcutValidationResult.Failure(configuration, $"Shortcut target does not exist: {targetPath}");
                    }

                    if (!string.Equals(targetPath, _genHubExecutablePath, StringComparison.OrdinalIgnoreCase))
                    {
                        return ShortcutValidationResult.Failure(configuration, 
                            $"Shortcut target mismatch. Expected: {_genHubExecutablePath}, Actual: {targetPath}");
                    }

                    return ShortcutValidationResult.Success(configuration, shortcutPath);
                }
                catch (Exception ex)
                {
                    return ShortcutValidationResult.Failure(configuration, $"Validation error: {ex.Message}");
                }
            }, cancellationToken);
        }

        /// <summary>
        /// Sanitizes a filename by removing invalid characters
        /// </summary>
        private static string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return "Shortcut";

            var invalidChars = Path.GetInvalidFileNameChars();
            foreach (var invalidChar in invalidChars)
            {
                fileName = fileName.Replace(invalidChar, '_');
            }

            return fileName.Trim();
        }
    }
}
