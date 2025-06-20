using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Interfaces.DesktopShortcuts;
using GenHub.Core.Interfaces;
using GenHub.Core.Models.Results;
using Microsoft.Extensions.Logging;

namespace GenHub.Features.DesktopShortcuts.Services
{
    /// <summary>
    /// Service for extracting icons from executable files for use in shortcuts
    /// </summary>
    public class ShortcutIconExtractor : IShortcutIconExtractor
    {
        private readonly IGameProfileManagerService _profileManager;
        private readonly ILogger<ShortcutIconExtractor> _logger;

        public ShortcutIconExtractor(
            IGameProfileManagerService profileManager,
            ILogger<ShortcutIconExtractor> logger)
        {
            _profileManager = profileManager ?? throw new ArgumentNullException(nameof(profileManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Extracts an icon from a game executable
        /// </summary>
        /// <param name="executablePath">Path to the executable</param>
        /// <param name="outputPath">Path where to save the extracted icon</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Operation result with the path to the extracted icon</returns>
        public async Task<OperationResult<string>> ExtractIconFromExecutableAsync(string executablePath, string outputPath, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Extracting icon from executable: {ExecutablePath}", executablePath);

                if (string.IsNullOrEmpty(executablePath))
                {
                    return OperationResult<string>.Failed("Executable path is required");
                }

                if (string.IsNullOrEmpty(outputPath))
                {
                    return OperationResult<string>.Failed("Output path is required");
                }

                if (!File.Exists(executablePath))
                {
                    return OperationResult<string>.Failed($"Executable file not found: {executablePath}");
                }

                // Ensure output directory exists
                var outputDirectory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(outputDirectory) && !Directory.Exists(outputDirectory))
                {
                    Directory.CreateDirectory(outputDirectory);
                }

                // Run icon extraction in a task to make it async
                var extractedPath = await Task.Run(() => ExtractIconInternal(executablePath, outputPath), cancellationToken);

                if (string.IsNullOrEmpty(extractedPath))
                {
                    // Fall back to default icon if extraction fails
                    var gameType = DetermineGameType(executablePath);
                    return await CreateDefaultIconAsync(gameType, outputPath, cancellationToken);
                }

                _logger.LogInformation("Successfully extracted icon to: {OutputPath}", extractedPath);
                return OperationResult<string>.Succeeded(extractedPath);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Icon extraction was cancelled for: {ExecutablePath}", executablePath);
                return OperationResult<string>.Failed("Icon extraction was cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extract icon from executable: {ExecutablePath}", executablePath);
                return OperationResult<string>.Failed($"Icon extraction failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the best available icon for a profile
        /// </summary>
        /// <param name="profileId">Profile ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Operation result with the path to the best icon</returns>
        public async Task<OperationResult<string>> GetBestIconForProfileAsync(string profileId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Getting best icon for profile: {ProfileId}", profileId);

                if (string.IsNullOrEmpty(profileId))
                {
                    return OperationResult<string>.Failed("Profile ID is required");
                }

                // Get profile information
                var profile = await _profileManager.GetProfileAsync(profileId, cancellationToken);
                if (profile == null)
                {
                    return OperationResult<string>.Failed($"Profile not found: {profileId}");
                }

                // Check if profile already has a valid icon
                if (!string.IsNullOrEmpty(profile.IconPath) && File.Exists(profile.IconPath))
                {
                    var validationResult = ValidateIcon(profile.IconPath);
                    if (validationResult.Success)
                    {
                        _logger.LogDebug("Using existing profile icon: {IconPath}", profile.IconPath);
                        return OperationResult<string>.Succeeded(profile.IconPath);
                    }
                }

                // Try to extract from executable
                if (!string.IsNullOrEmpty(profile.ExecutablePath) && File.Exists(profile.ExecutablePath))
                {
                    var outputPath = GetDefaultIconPath(profileId, profile.Name);
                    var extractResult = await ExtractIconFromExecutableAsync(profile.ExecutablePath, outputPath, cancellationToken);
                    if (extractResult.Success)
                    {
                        return extractResult;
                    }
                }

                // Fall back to default icon
                var gameType = profile.Name?.Contains("Zero Hour") == true ? "Zero Hour" : "Generals";
                var defaultPath = GetDefaultIconPath(profileId, profile.Name);
                return await CreateDefaultIconAsync(gameType, defaultPath, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get best icon for profile: {ProfileId}", profileId);
                return OperationResult<string>.Failed($"Failed to get icon: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates a default icon for a profile type
        /// </summary>
        /// <param name="profileType">Type of profile (e.g., "Generals", "Zero Hour")</param>
        /// <param name="outputPath">Path where to save the icon</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Operation result with the path to the created icon</returns>
        public async Task<OperationResult<string>> CreateDefaultIconAsync(string profileType, string outputPath, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Creating default icon for profile type: {ProfileType}", profileType);

                if (string.IsNullOrEmpty(outputPath))
                {
                    return OperationResult<string>.Failed("Output path is required");
                }

                // Ensure output directory exists
                var outputDirectory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(outputDirectory) && !Directory.Exists(outputDirectory))
                {
                    Directory.CreateDirectory(outputDirectory);
                }

                var defaultIconPath = await Task.Run(() => GetDefaultIconForType(profileType), cancellationToken);
                
                if (string.IsNullOrEmpty(defaultIconPath) || !File.Exists(defaultIconPath))
                {
                    return OperationResult<string>.Failed($"No default icon available for profile type: {profileType}");
                }

                // Copy default icon to output path
                var finalOutputPath = Path.ChangeExtension(outputPath, Path.GetExtension(defaultIconPath));
                File.Copy(defaultIconPath, finalOutputPath, overwrite: true);

                _logger.LogInformation("Created default icon: {OutputPath}", finalOutputPath);
                return OperationResult<string>.Succeeded(finalOutputPath);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Default icon creation was cancelled for type: {ProfileType}", profileType);
                return OperationResult<string>.Failed("Icon creation was cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create default icon for type: {ProfileType}", profileType);
                return OperationResult<string>.Failed($"Failed to create default icon: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates that an icon file exists and is accessible
        /// </summary>
        /// <param name="iconPath">Path to the icon file</param>
        /// <returns>Validation result</returns>
        public OperationResult ValidateIcon(string iconPath)
        {
            try
            {
                if (string.IsNullOrEmpty(iconPath))
                {
                    return OperationResult.Failed("Icon path is null or empty");
                }

                if (!File.Exists(iconPath))
                {
                    return OperationResult.Failed($"Icon file not found: {iconPath}");
                }

                // Check if file is readable
                using (var stream = File.OpenRead(iconPath))
                {
                    if (stream.Length == 0)
                    {
                        return OperationResult.Failed("Icon file is empty");
                    }
                }

                // Validate file extension
                var extension = Path.GetExtension(iconPath).ToLowerInvariant();
                if (extension != ".ico" && extension != ".png" && extension != ".jpg" && extension != ".jpeg" && extension != ".bmp")
                {
                    return OperationResult.Failed($"Unsupported icon format: {extension}");
                }

                _logger.LogDebug("Icon validation passed: {IconPath}", iconPath);
                return OperationResult.Succeeded("Icon is valid");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to validate icon: {IconPath}", iconPath);
                return OperationResult.Failed($"Icon validation failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the default icon path for a profile
        /// </summary>
        /// <param name="profileId">Profile ID</param>
        /// <param name="profileName">Profile name</param>
        /// <returns>Path to the default icon</returns>
        private string GetDefaultIconPath(string profileId, string? profileName)
        {
            try
            {
                if (string.IsNullOrEmpty(profileId))
                {
                    throw new ArgumentException("Profile ID is required", nameof(profileId));
                }

                // Create a safe filename from profile name
                var safeName = string.IsNullOrEmpty(profileName) ? profileId : profileName;
                safeName = string.Join("_", safeName.Split(Path.GetInvalidFileNameChars()));

                // Use the GenHub icons directory
                var iconsDirectory = GetIconsDirectory();
                var iconPath = Path.Combine(iconsDirectory, $"{safeName}.png");

                _logger.LogDebug("Generated default icon path for profile {ProfileId}: {IconPath}", profileId, iconPath);
                return iconPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get default icon path for profile: {ProfileId}", profileId);
                throw;
            }
        }

        /// <summary>
        /// Internal method for extracting icon from executable
        /// </summary>
        private string? ExtractIconInternal(string executablePath, string outputPath)
        {
            try
            {
                // For now, we'll use a placeholder implementation
                // In a full implementation, this would use platform-specific icon extraction
                _logger.LogWarning("Icon extraction not fully implemented. Using default icon fallback.");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during internal icon extraction");
                return null;
            }
        }

        /// <summary>
        /// Determines game type from executable path
        /// </summary>
        private string DetermineGameType(string executablePath)
        {
            var filename = Path.GetFileNameWithoutExtension(executablePath).ToLowerInvariant();
            
            if (filename.Contains("zeronour") || filename.Contains("generals2") || filename == "game")
            {
                return "Zero Hour";
            }
            
            if (filename.Contains("generals") || filename == "game")
            {
                return "Generals";
            }

            return "Unknown";
        }

        /// <summary>
        /// Gets the default icon for a specific game type
        /// </summary>
        private string? GetDefaultIconForType(string profileType)
        {
            try
            {
                // Look for type-specific icons first
                var currentDirectory = AppDomain.CurrentDomain.BaseDirectory;
                var possiblePaths = new[]
                {
                    Path.Combine(currentDirectory, "Assets", "Icons", $"{profileType.ToLowerInvariant().Replace(" ", "_")}.png"),
                    Path.Combine(currentDirectory, "Assets", "Icons", "generals.png"),
                    Path.Combine(currentDirectory, "Assets", "Icons", "genhub.png"),
                    Path.Combine(currentDirectory, "Assets", "Icons", "default.png"),
                    Path.Combine(currentDirectory, "Assets", "placeholder.png")
                };

                foreach (var path in possiblePaths)
                {
                    if (File.Exists(path))
                    {
                        return path;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to find default icon for type: {ProfileType}", profileType);
                return null;
            }
        }

        /// <summary>
        /// Gets the GenHub icons directory
        /// </summary>
        private string GetIconsDirectory()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var iconsPath = Path.Combine(appDataPath, "GenHub", "Icons");
            
            if (!Directory.Exists(iconsPath))
            {
                Directory.CreateDirectory(iconsPath);
            }

            return iconsPath;
        }
    }
}
