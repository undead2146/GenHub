using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Interfaces;
using GenHub.Core.Models;
using GenHub.Core.Models.GameProfiles;
using Microsoft.Extensions.Logging;

namespace GenHub.Features.GameVersions.Services
{
    /// <summary>
    /// Facade service for game detection across different platforms
    /// </summary>
    public class GameDetectionFacade : IGameDetector, IGameExecutableLocator
    {
        private readonly ILogger<GameDetectionFacade> _logger;
        private readonly IGameDetector _gameDetector;
        private readonly IGameExecutableLocator _gameExecutableLocator;
        private readonly string _versionsPath;
        private readonly JsonSerializerOptions _jsonOptions;

        public GameDetectionFacade(
            ILogger<GameDetectionFacade> logger,
            IGameDetector gameDetector,
            IGameExecutableLocator gameExecutableLocator,
            string versionsPath,
            JsonSerializerOptions jsonOptions) // Add JsonSerializerOptions parameter
        {
            _logger = logger;
            _gameDetector = gameDetector;
            _gameExecutableLocator = gameExecutableLocator;
            _versionsPath = versionsPath;
            _jsonOptions = jsonOptions;
        }

        /// <summary>
        /// Creates GameVersion objects from detected game installations
        /// </summary>
        public async Task<List<GameVersion>> CreateGameVersionsFromDetectedInstallationsAsync(CancellationToken cancellationToken = default)
        {
            var result = new List<GameVersion>();
            
            try
            {
                _logger.LogInformation("Detecting game installations");
                
                // Get standard installations from platform-specific detector
                var installations = await _gameDetector.DetectInstallationsAsync(cancellationToken);
                var detectedVersions = await CreateVersionsFromInstallationsAsync(installations, cancellationToken);
                result.AddRange(detectedVersions);
                
                // Scan local version directory for GitHub installations
                await ScanCustomVersionsDirectory(result, cancellationToken);
                
                _logger.LogInformation("Created {Count} versions from detected installations", result.Count);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating versions from detected installations");
                return result;
            }
        }

        /// <summary>
        /// Creates versions from detected installations asynchronously
        /// </summary>
        private async Task<List<GameVersion>> CreateVersionsFromInstallationsAsync(
            IEnumerable<IGameInstallation> installations,
            CancellationToken cancellationToken)
        {
            var result = new List<GameVersion>();
            
            if (installations == null)
                return result;
            
            // Use a list to collect all version creation tasks
            var tasks = new List<Task<GameVersion?>>();
            
            foreach (var installation in installations)
            {
                try
                {
                    _logger.LogDebug("Processing installation: {Type}", installation.InstallationType);
                    
                    // Process Vanilla Generals installations
                    if (installation.IsVanillaInstalled && !string.IsNullOrEmpty(installation.VanillaGamePath))
                    {
                        tasks.Add(TryCreateVersionFromPathAsync(
                            installation.VanillaGamePath, 
                            installation.InstallationType, 
                            false, 
                            cancellationToken));
                    }
                    
                    // Process Zero Hour installations
                    if (installation.IsZeroHourInstalled && !string.IsNullOrEmpty(installation.ZeroHourGamePath))
                    {
                        tasks.Add(TryCreateVersionFromPathAsync(
                            installation.ZeroHourGamePath, 
                            installation.InstallationType, 
                            true, 
                            cancellationToken));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing installation type: {Type}", installation.InstallationType);
                }
            }
            
            // Wait for all tasks to complete and collect results
            var versions = await Task.WhenAll(tasks);
            
            // Add non-null versions that aren't duplicates
            foreach (var version in versions)
            {
                if (version != null && !IsDuplicate(version, result))
                {
                    result.Add(version);
                }
            }
            
            return result;
        }

        /// <summary>
        /// Tries to create a GameVersion from a given installation path and type asynchronously
        /// </summary>
        private async Task<GameVersion?> TryCreateVersionFromPathAsync(
            string gameDirectoryPath, 
            GameInstallationType installationType, 
            bool preferZeroHour, 
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(gameDirectoryPath) || !Directory.Exists(gameDirectoryPath))
            {
                _logger.LogWarning("Provided game directory path is invalid or does not exist: {Path}", gameDirectoryPath);
                return null;
            }
            
            try
            {
                // Use await properly instead of Task.Run(...).GetAwaiter().GetResult()
                string executablePath = await _gameExecutableLocator.FindBestGameExecutableAsync(
                    gameDirectoryPath, preferZeroHour, cancellationToken);
                
                if (string.IsNullOrEmpty(executablePath) || !File.Exists(executablePath) || 
                    !_gameExecutableLocator.IsValidGameExecutable(executablePath))
                {
                    _logger.LogWarning("No valid game executable found in directory: {Path} for installation type {InstallationType}", 
                        gameDirectoryPath, installationType);
                    return null;
                }
                
                // Get executable info from the executable locator
                GameVersion? executableInfo = _gameExecutableLocator.GetExecutableInfo(executablePath);
                if (executableInfo == null)
                {
                    _logger.LogWarning("Could not get executable info for: {Path}", executablePath);
                    return null;
                }
                
                // Augment with installation-specific information
                executableInfo.SourceType = installationType;
                executableInfo.InstallPath = gameDirectoryPath; // Store the actual installation directory
                
                // Ensure name includes the installation type if it doesn't already
                string originalName = executableInfo.Name ?? executableInfo.GameType ?? "Unknown";
                if (!originalName.Contains(installationType.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    executableInfo.Name = $"{originalName} ({installationType})";
                }
                
                _logger.LogInformation("Created version from path: {Name}, Executable: {ExecutablePath}, InstallPath: {InstallPath}",
                    executableInfo.Name, executableInfo.ExecutablePath, executableInfo.InstallPath);
                return executableInfo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating version from game directory path {GameDirectoryPath} for installation type {InstallationType}", 
                    gameDirectoryPath, installationType);
                return null;
            }
        }

        /// <summary>
        /// Scans the versions directory for custom installations
        /// </summary>
        private async Task ScanCustomVersionsDirectory(List<GameVersion> result, CancellationToken cancellationToken)
        {
            try
            {
                if (string.IsNullOrEmpty(_versionsPath) || !Directory.Exists(_versionsPath))
                {
                    _logger.LogWarning("Versions directory does not exist: {Path}", _versionsPath);
                    return;
                }
                
                // Scan GitHub directory
                var githubPath = Path.Combine(_versionsPath, "GitHub");
                if (Directory.Exists(githubPath))
                {
                    await ScanGitHubDirectory(githubPath, result, cancellationToken);
                }
                
                // Scan Local directory
                var localPath = Path.Combine(_versionsPath, "Local");
                if (Directory.Exists(localPath))
                {
                    await ScanLocalDirectory(localPath, result, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scanning versions directory");
            }
        }

        /// <summary>
        /// Scans GitHub directory for installations
        /// </summary>
        private async Task ScanGitHubDirectory(string githubPath, List<GameVersion> result, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Scanning GitHub versions directory: {Path}", githubPath);
                
                // GitHub structure: GitHub/{RepoOwner}/{RepoName}/{InstallName}
                foreach (var repoOwnerDir in Directory.GetDirectories(githubPath))
                {
                    foreach (var repoNameDir in Directory.GetDirectories(repoOwnerDir))
                    {
                        foreach (var installDir in Directory.GetDirectories(repoNameDir))
                        {
                            await ProcessInstallDirectory(installDir, GameInstallationType.GitHubArtifact, result, cancellationToken);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scanning GitHub directory");
            }
        }
        
        /// <summary>
        /// Scans Local directory for installations
        /// </summary>
        private async Task ScanLocalDirectory(string localPath, List<GameVersion> result, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Scanning Local versions directory: {Path}", localPath);
                
                foreach (var installDir in Directory.GetDirectories(localPath))
                {
                    await ProcessInstallDirectory(installDir, GameInstallationType.LocalZipFile, result, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scanning Local directory");
            }
        }
        
        /// <summary>
        /// Processes a single installation directory
        /// </summary>
        private async Task ProcessInstallDirectory(string installDir, GameInstallationType sourceType, List<GameVersion> result, CancellationToken cancellationToken)
        {
            try
            {
                // First try to load from JSON metadata
                string dirName = Path.GetFileName(installDir);
                string jsonPath = Path.Combine(installDir, $"{dirName}.json");
                
                if (File.Exists(jsonPath))
                {
                    try
                    {
                        _logger.LogDebug("Found metadata file: {JsonFile}", jsonPath);
                        string json = await File.ReadAllTextAsync(jsonPath, cancellationToken);
                        var version = JsonSerializer.Deserialize<GameVersion>(json, _jsonOptions); // Use shared JsonSerializerOptions
                        
                        if (version != null && !string.IsNullOrEmpty(version.ExecutablePath) && File.Exists(version.ExecutablePath))
                        {
                            _logger.LogInformation("Successfully parsed version from metadata: {Name}", version.Name);
                            
                            // Ensure correct source type
                            version.SourceType = sourceType;
                            
                            // Check for duplicates
                            if (!IsDuplicate(version, result))
                            {
                                result.Add(version);
                                return; // Exit early if we loaded from metadata
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error parsing metadata file: {JsonFile}", jsonPath);
                    }
                }
                
                // Fallback to executable scanning - use modified logic for GitHub installations
                if (sourceType == GameInstallationType.GitHubArtifact)
                {
                    // For GitHub installations, specifically look for GitHub executables first
                    var githubExecutables = Directory.GetFiles(installDir, "*.exe")
                        .Where(file => {
                            string fileName = Path.GetFileName(file).ToLowerInvariant();
                            return fileName == "generalsv.exe" || fileName == "generalszh.exe";
                        })
                        .ToList();
                    
                    if (githubExecutables.Any())
                    {
                        // Process only the GitHub executables
                        foreach (var exePath in githubExecutables)
                        {
                            var exeInfo = _gameExecutableLocator.GetExecutableInfo(exePath);
                            if (exeInfo != null)
                            {
                                // Set source type and path information
                                exeInfo.SourceType = sourceType;
                                exeInfo.InstallPath = installDir;
                                ApplyVersionNameFromDirectory(exeInfo, installDir, sourceType);
                                
                                // Check for duplicates before adding
                                if (!IsDuplicate(exeInfo, result))
                                {
                                    result.Add(exeInfo);
                                }
                            }
                        }
                        
                        // If we found and processed GitHub executables, don't continue with regular scanning
                        return;
                    }
                }
                
                // Standard executable scanning for non-GitHub installations or when no GitHub executables found
                var installVersions = _gameExecutableLocator.ScanDirectoryForExecutables(installDir);
                
                foreach (var version in installVersions)
                {
                    // Set source type
                    version.SourceType = sourceType;
                    
                    // Only apply custom naming if the GameExecutableLocator didn't set a meaningful name
                    // or if the directory contains valuable context not captured by the executable info
                    if (string.IsNullOrEmpty(version.Name) || 
                        version.Name == Path.GetFileNameWithoutExtension(version.ExecutablePath) ||
                        (sourceType == GameInstallationType.GitHubArtifact && !version.Name.Contains("/")))
                    {
                        ApplyVersionNameFromDirectory(version, installDir, sourceType);
                    }
                    
                    // Check for duplicates
                    if (!IsDuplicate(version, result))
                    {
                        result.Add(version);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing installation directory: {Directory}", installDir);
            }
        }
        
        /// <summary>
        /// Applies a name to a version based on the directory structure
        /// Used as a fallback when GameExecutableLocator's naming is insufficient
        /// </summary>
        private void ApplyVersionNameFromDirectory(GameVersion version, string installDir, GameInstallationType sourceType)
        {
            // If version already has a meaningful name (not just the executable name), don't override it
            if (!string.IsNullOrEmpty(version.Name) && 
                (!string.IsNullOrEmpty(version.ExecutablePath) && 
                 version.Name != Path.GetFileNameWithoutExtension(version.ExecutablePath)))
            {
                // Only augment existing name with directory context if it would add value
                string gameType = version.GameType ?? (version.IsZeroHour ? "Zero Hour" : "Generals");
                
                if (sourceType == GameInstallationType.GitHubArtifact && !version.Name.Contains("/"))
                {
                    // Extract path components for GitHub builds to add repository context
                    var parts = installDir.Split(Path.DirectorySeparatorChar);
                    
                    if (parts.Length >= 3)
                    {
                        string repoOwner = parts[parts.Length - 3];
                        string repoName = parts[parts.Length - 2];
                        
                        // Add repository context to the existing name
                        version.Name = $"{version.Name} ({repoOwner}/{repoName})";
                    }
                }
                
                return;
            }
            
            // Apply name based on directory structure when no meaningful name exists
            if (sourceType == GameInstallationType.GitHubArtifact)
            {
                // Extract path components for GitHub builds
                var parts = installDir.Split(Path.DirectorySeparatorChar);
                
                if (parts.Length >= 3)
                {
                    string repoOwner = parts[parts.Length - 3];
                    string repoName = parts[parts.Length - 2];
                    string dirName = parts[parts.Length - 1];
                    
                    string gameType = version.GameType ?? (version.IsZeroHour ? "Zero Hour" : "Generals");
                    version.Name = $"{gameType} - {repoOwner}/{repoName}/{dirName}";
                }
            }
            else
            {
                // Format for local installations
                string dirName = Path.GetFileName(installDir);
                string gameType = version.GameType ?? (version.IsZeroHour ? "Zero Hour" : "Generals");
                version.Name = $"{gameType} - {dirName}";
            }
        }
        
        /// <summary>
        /// Checks if a version is a duplicate, considering path and source type
        /// </summary>
        private bool IsDuplicate(GameVersion version, List<GameVersion> existingVersions)
        {
            // More sophisticated duplicate detection that prioritizes GitHub artifacts
            var existingVersion = existingVersions.FirstOrDefault(v => 
                !string.IsNullOrEmpty(v.ExecutablePath) &&
                !string.IsNullOrEmpty(version.ExecutablePath) &&
                v.ExecutablePath.Equals(version.ExecutablePath, StringComparison.OrdinalIgnoreCase));
                
            if (existingVersion == null)
                return false;
                
            // If we're adding a GitHub artifact and found an existing version that's not a GitHub artifact,
            // replace the existing one with the GitHub version
            if (version.SourceType == GameInstallationType.GitHubArtifact && 
                existingVersion.SourceType != GameInstallationType.GitHubArtifact)
            {
                existingVersions.Remove(existingVersion);
                return false; // Not considered a duplicate, we'll add the GitHub version
            }
            
            return true; // Otherwise it's a duplicate
        }

        /// <inheritdoc />
        public async Task<IEnumerable<IGameInstallation>> DetectInstallationsAsync(CancellationToken cancellationToken = default)
        {
            return await _gameDetector.DetectInstallationsAsync(cancellationToken);
        }

        /// <inheritdoc />
        public async Task<string> FindExecutableAsync(string directory, CancellationToken cancellationToken = default)
        {
            return await _gameExecutableLocator.FindExecutableAsync(directory, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<string> FindBestGameExecutableAsync(string directory, bool preferZeroHour = false, CancellationToken cancellationToken = default)
        {
            return await _gameExecutableLocator.FindBestGameExecutableAsync(directory, preferZeroHour, cancellationToken);
        }

        /// <inheritdoc />
        public bool IsZeroHourDirectory(string directory)
        {
            return _gameExecutableLocator.IsZeroHourDirectory(directory);
        }

        /// <inheritdoc />
        public async Task<(bool Success, string? ExecutablePath)> FindGameExecutableAsync(string directory, CancellationToken cancellationToken = default)
        {
            return await _gameExecutableLocator.FindGameExecutableAsync(directory, cancellationToken);
        }

        /// <inheritdoc />
        public List<GameVersion> ScanDirectoryForExecutables(string directory)
        {
            return _gameExecutableLocator.ScanDirectoryForExecutables(directory);
        }

        /// <inheritdoc />
        public GameVersion? GetExecutableInfo(string executablePath)
        {
            return _gameExecutableLocator.GetExecutableInfo(executablePath);
        }

        /// <inheritdoc />
        public bool IsValidGameExecutable(string filePath)
        {
            return _gameExecutableLocator.IsValidGameExecutable(filePath);
        }
    }
}
