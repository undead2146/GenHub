using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Interfaces;
using GenHub.Core.Models.GameProfiles;
using Microsoft.Extensions.Logging;

namespace GenHub.Features.GameVersions.Services
{
    /// <summary>
    /// Service for discovering game versions from various sources
    /// </summary>
    public class GameVersionDiscoveryService : IGameVersionDiscoveryService
    {
        private readonly ILogger<GameVersionDiscoveryService> _logger;
        private readonly IGameDetector _gameDetector;
        private readonly IGameExecutableLocator _gameExecutableLocator;
        private readonly string _versionsPath;
        private readonly GameDetectionFacade _gameDetectionFacade;
        private readonly IGameVersionManager _gameVersionManager;

        /// <summary>
        /// Creates a new instance of GameVersionDiscoveryService
        /// </summary>
        public GameVersionDiscoveryService(
            ILogger<GameVersionDiscoveryService> logger,
            IGameDetector gameDetector,
            IGameExecutableLocator gameExecutableLocator,
            GameDetectionFacade gameDetectionFacade,
            IGameVersionManager gameVersionManager)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _gameDetector = gameDetector ?? throw new ArgumentNullException(nameof(gameDetector));
            _gameExecutableLocator = gameExecutableLocator ?? throw new ArgumentNullException(nameof(gameExecutableLocator));
            _gameDetectionFacade = gameDetectionFacade ?? throw new ArgumentNullException(nameof(gameDetectionFacade));
            _gameVersionManager = gameVersionManager ?? throw new ArgumentNullException(nameof(gameVersionManager));
            _versionsPath = _gameVersionManager.GetVersionsStoragePath();
        }

        /// <summary>
        /// Discovers available game versions from all sources and saves them
        /// </summary>
        public async Task<IEnumerable<GameVersion>> DiscoverVersionsAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Discovering game versions from all sources");
            
            try
            {
                // Get detected versions without saving
                var detectedVersions = await GetDetectedVersionsAsync(cancellationToken);
                int versionsCount = detectedVersions.Count();
                _logger.LogInformation("Discovered {Count} versions", versionsCount);
                
                // Save each detected version
                foreach (var version in detectedVersions)
                {
                    await _gameVersionManager.SaveVersionAsync(version, cancellationToken);
                }
                
                // Return all installed versions now that we've saved the detected ones
                return await _gameVersionManager.GetInstalledVersionsAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error discovering versions");
                return new List<GameVersion>();
            }
        }

        /// <summary>
        /// Gets detected versions without saving them
        /// </summary>
        public async Task<IEnumerable<GameVersion>> GetDetectedVersionsAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Detecting game versions without saving");
            
            try
            {
                // Use the detection facade to find installations
                var detectedVersions = await _gameDetectionFacade.CreateGameVersionsFromDetectedInstallationsAsync(cancellationToken);
                
                _logger.LogInformation("Detected {Count} versions", detectedVersions.Count);
                return detectedVersions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting versions");
                return new List<GameVersion>();
            }
        }

        /// <summary>
        /// Gets default game versions from standard installation locations
        /// </summary>
        public async Task<IEnumerable<GameVersion>> GetDefaultGameVersionsAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Getting default game versions");
            
            try
            {
                // Detect installations using the updated interface method
                var installations = await _gameDetector.DetectInstallationsAsync(cancellationToken);
                
                if (!installations.Any())
                {
                    _logger.LogWarning("No installations detected");
                    return new List<GameVersion>();
                }
                
                var result = new List<GameVersion>();
                
                foreach (var installation in installations)
                {
                    // Process Generals (Vanilla) installations
                    if (installation.IsVanillaInstalled && !string.IsNullOrEmpty(installation.VanillaGamePath))
                    {
                        var executablePath = await _gameExecutableLocator.FindExecutableAsync(
                            installation.VanillaGamePath, cancellationToken);
                        
                        if (!string.IsNullOrEmpty(executablePath) && File.Exists(executablePath))
                        {
                            var version = new GameVersion
                            {
                                Id = Guid.NewGuid().ToString(),
                                Name = $"Generals ({installation.InstallationType})",
                                ExecutablePath = executablePath,
                                InstallPath = installation.VanillaGamePath,
                                GameType = "Generals",
                                IsZeroHour = false,
                                SourceType = installation.InstallationType
                            };
                            
                            result.Add(version);
                        }
                    }
                    
                    // Process Zero Hour installations
                    if (installation.IsZeroHourInstalled && !string.IsNullOrEmpty(installation.ZeroHourGamePath))
                    {
                        var executablePath = await _gameExecutableLocator.FindExecutableAsync(
                            installation.ZeroHourGamePath, cancellationToken);
                        
                        if (!string.IsNullOrEmpty(executablePath) && File.Exists(executablePath))
                        {
                            var version = new GameVersion
                            {
                                Id = Guid.NewGuid().ToString(),
                                Name = $"Zero Hour ({installation.InstallationType})",
                                ExecutablePath = executablePath,
                                InstallPath = installation.ZeroHourGamePath,
                                GameType = "Zero Hour",
                                IsZeroHour = true,
                                SourceType = installation.InstallationType
                            };
                            
                            result.Add(version);
                        }
                    }
                }
                
                _logger.LogInformation("Found {Count} default game versions", result.Count);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting default game versions");
                return new List<GameVersion>();
            }
        }

        /// <summary>
        /// Scans a specific directory for game versions
        /// </summary>
        public async Task<IEnumerable<GameVersion>> ScanDirectoryForVersionsAsync(string directoryPath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(directoryPath) || !Directory.Exists(directoryPath))
            {
                _logger.LogWarning("Invalid directory path: {Path}", directoryPath);
                return new List<GameVersion>();
            }
            
            _logger.LogInformation("Scanning directory for versions: {Directory}", directoryPath);
            
            try
            {
                // First check for JSON metadata
                string dirName = Path.GetFileName(directoryPath);
                string jsonPath = Path.Combine(directoryPath, $"{dirName}.json");
                
                if (File.Exists(jsonPath))
                {
                    try
                    {
                        _logger.LogDebug("Found metadata file: {JsonFile}", jsonPath);
                        string json = await File.ReadAllTextAsync(jsonPath, cancellationToken);
                        var version = JsonSerializer.Deserialize<GameVersion>(json);
                        
                        if (version != null && !string.IsNullOrEmpty(version.ExecutablePath) && File.Exists(version.ExecutablePath))
                        {
                            _logger.LogInformation("Successfully parsed version from metadata: {Name}", version.Name);
                            return new List<GameVersion> { version };
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error parsing metadata file: {JsonFile}", jsonPath);
                    }
                }
                
                // Fallback to executable scanning
                var scannedVersions = _gameExecutableLocator.ScanDirectoryForExecutables(directoryPath);
                _logger.LogInformation("Found {Count} versions in directory", scannedVersions.Count);
                
                return scannedVersions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scanning directory for versions: {Directory}", directoryPath);
                return new List<GameVersion>();
            }
        }

        /// <summary>
        /// Validates that a version can be used (exists, has valid executable)
        /// </summary>
        public async Task<bool> ValidateVersionAsync(GameVersion version, CancellationToken cancellationToken = default)
        {
            if (version == null)
                return false;
                
            try
            {
                _logger.LogDebug("Validating version: {VersionId}, {Name}", version.Id, version.Name);
                
                // Check if executable exists
                if (string.IsNullOrEmpty(version.ExecutablePath) || !File.Exists(version.ExecutablePath))
                {
                    _logger.LogWarning("Version has invalid executable path: {Path}", version.ExecutablePath);
                    
                    // If we have an install path but no executable, try to find one
                    if (!string.IsNullOrEmpty(version.InstallPath) && Directory.Exists(version.InstallPath))
                    {
                        var (success, executablePath) = await _gameExecutableLocator.FindGameExecutableAsync(
                            version.InstallPath, cancellationToken);
                        
                        if (success && !string.IsNullOrEmpty(executablePath))
                        {
                            _logger.LogInformation("Found alternative executable for version: {Path}", executablePath);
                            // Update the executable path
                            version.ExecutablePath = executablePath;
                            
                            // Save the updated version
                            await _gameVersionManager.UpdateVersionAsync(version, cancellationToken);
                            
                            return true;
                        }
                    }
                    
                    return false;
                }
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating version: {VersionId}", version.Id);
                return false;
            }
        }
    }
}
