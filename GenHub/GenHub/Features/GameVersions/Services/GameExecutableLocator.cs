using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using GenHub.Core.Interfaces;
using GenHub.Core.Models;
using GenHub.Core.Models.GameProfiles;

namespace GenHub.Features.GameVersions.Services
{
    /// <summary>
    /// Service for locating and identifying game executables
    /// </summary>
    public class GameExecutableLocator : IGameExecutableLocator
    {
        private readonly ILogger<GameExecutableLocator> _logger;
        
        // Known retail installation types where we should strictly enforce generals.exe only
        private static readonly HashSet<GameInstallationType> _retailInstallationTypes = new()
        {
            GameInstallationType.Steam,
            GameInstallationType.EaApp,
            GameInstallationType.Origin,
            GameInstallationType.TheFirstDecade
        };

        // Explicitly define game executables - strict list of ONLY the main game executables
        private static readonly HashSet<string> _validGameExecutables = new(StringComparer.OrdinalIgnoreCase)
        {
            "generals.exe",      // Standard game executable for both vanilla and ZH
            "generalszh.exe",    // GitHub ZH build
            "generalsv.exe",     // GitHub vanilla build
            "generals_zh.exe",   // Some mod versions
            "generalzh.exe"      // Alternative ZH spelling
        };

        // Utility executables that should NEVER be used as main game executables
        private static readonly HashSet<string> _utilityExecutables = new(StringComparer.OrdinalIgnoreCase)
        {
            "assetcull.exe",
            "binkw32.exe",
            "eauninstall.exe",
            "ea_logo.exe",
            "eagames.exe",
            "gamespy.exe",
            "options.exe",
            "upgrade.exe",
            "worldbuilder.exe"
        };

        // Known executable names with their game type mapping and priority
        private readonly Dictionary<string, ExecutableInfo> _knownExecutables = new(StringComparer.OrdinalIgnoreCase)
        {
            // Standard installations
            { "generals.exe", new ExecutableInfo("Generals", false, GameInstallationType.Unknown, 1) },
            
            // GitHub builds 
            { "generalsv.exe", new ExecutableInfo("Generals", false, GameInstallationType.GitHubArtifact, 3) },
            { "generalszh.exe", new ExecutableInfo("Zero Hour", true, GameInstallationType.GitHubArtifact, 3) },

            // Zero Hour variants
            { "generals_zh.exe", new ExecutableInfo("Zero Hour", true, GameInstallationType.Unknown, 2) },
            { "generalzh.exe", new ExecutableInfo("Zero Hour", true, GameInstallationType.Unknown, 2) }
        };

        public GameExecutableLocator(ILogger<GameExecutableLocator> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Checks if a file is a valid game executable (strictly enforced)
        /// </summary>
        public bool IsValidGameExecutable(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return false;
                
            string fileName = Path.GetFileName(filePath).ToLowerInvariant();
            
            // First, explicitly check against our blacklist of utility executables
            if (_utilityExecutables.Contains(fileName))
            {
                _logger.LogDebug("Rejected utility executable: {Executable}", fileName);
                return false;
            }
            
            // Then check against our whitelist of valid game executables
            if (_validGameExecutables.Contains(fileName))
            {
                _logger.LogDebug("Accepted valid game executable: {Executable}", fileName);
                return true;
            }
            
            // Reject anything else by default
            _logger.LogDebug("Rejected unknown executable: {Executable}", fileName);
            return false;
        }

        /// <summary>
        /// Uses strict rules to determine if a directory contains a Zero Hour installation
        /// </summary>
        public bool IsZeroHourDirectory(string directory)
        {
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                return false;

            try
            {
                _logger.LogDebug("Checking if directory is Zero Hour: {Directory}", directory);
                
                // For retail installations, use specific folder name checks
                var sourceType = DetermineSourceType(directory);
                if (_retailInstallationTypes.Contains(sourceType))
                {
                    // For EA App and Steam, we need stricter rules - the name must explicitly have "Zero Hour" in it
                    string dirName = Path.GetFileName(directory).ToLowerInvariant();
                    
                    if (dirName.Contains("zero hour") || dirName.Equals("command and conquer generals zero hour"))
                    {
                        _logger.LogDebug("Retail directory confirmed as Zero Hour based on name: {DirName}", dirName);
                        return true;
                    }
                    
                    // For subdirectories, we need to check the parent directory name too
                    string? parentDir = Path.GetDirectoryName(directory);
                    if (!string.IsNullOrEmpty(parentDir))
                    {
                        string parentName = Path.GetFileName(parentDir).ToLowerInvariant();
                        if (parentName.Contains("zero hour"))
                        {
                            _logger.LogDebug("Retail directory confirmed as Zero Hour based on parent name: {ParentName}", parentName);
                            return true;
                        }
                    }
                    
                    // If it doesn't explicitly say "Zero Hour", assume it's the base game for retail
                    _logger.LogDebug("Retail directory detected without 'Zero Hour' in name - assuming base game");
                    return false;
                }
                
                // For GitHub and custom installations:
                
                // First, check for ZH-specific executables - best indicator
                if (File.Exists(Path.Combine(directory, "generalszh.exe")) ||
                    File.Exists(Path.Combine(directory, "generals_zh.exe")) ||
                    File.Exists(Path.Combine(directory, "generalzh.exe")))
                {
                    _logger.LogDebug("Found Zero Hour executable in directory");
                    return true;
                }
                
                // Check for Zero Hour in directory name (only for non-retail)
                if (directory.Contains("zerohour", StringComparison.OrdinalIgnoreCase) ||
                    directory.Contains("zero hour", StringComparison.OrdinalIgnoreCase) ||
                    (directory.Contains("zh", StringComparison.OrdinalIgnoreCase) && 
                     !directory.Contains("github", StringComparison.OrdinalIgnoreCase))) // Avoid false positives with GitHub paths
                {
                    _logger.LogDebug("Directory name indicates Zero Hour: {Directory}", directory);
                    return true;
                }

                // Check for ZH-specific data files
                var bigFiles = Directory.GetFiles(directory, "*.big", SearchOption.TopDirectoryOnly)
                    .Where(f => Path.GetFileName(f).StartsWith("zh", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                    
                if (bigFiles.Any())
                {
                    _logger.LogDebug("Found Zero Hour .big files in directory");
                    return true;
                }

                _logger.LogDebug("Directory does not appear to be Zero Hour: {Directory}", directory);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if directory is Zero Hour: {Directory}", directory);
                return false;
            }
        }

        /// <summary>
        /// Scans a directory for game executables, with strict rules based on installation type
        /// </summary>
        public List<GameVersion> ScanDirectoryForExecutables(string directory)
        {
            var result = new List<GameVersion>();
            
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                return result;

            try
            {
                _logger.LogDebug("Scanning for executables in: {Directory}", directory);
                
                // Determine source type from path
                var directorySourceType = DetermineSourceType(directory);
                _logger.LogDebug("Directory source type determined as: {SourceType}", directorySourceType);
                
                // *** RETAIL INSTALLATIONS (Steam, EA App, Origin, TFD) ***
                if (_retailInstallationTypes.Contains(directorySourceType))
                {
                    _logger.LogDebug("Retail installation detected - strictly looking for generals.exe only");
                    
                    string generalsExePath = Path.Combine(directory, "generals.exe");
                    if (File.Exists(generalsExePath))
                    {
                        // For retail installations, explicitly determine if it's Zero Hour based on directory name
                        bool isZeroHourDir = IsZeroHourDirectory(directory);
                        
                        var gameVersion = new GameVersion
                        {
                            Id = Guid.NewGuid().ToString(),
                            ExecutablePath = generalsExePath,
                            InstallPath = directory,
                            SourceType = directorySourceType,
                            IsZeroHour = isZeroHourDir,
                            GameType = isZeroHourDir ? "Zero Hour" : "Generals"
                        };
                        
                        // Format name consistently based on source type and game variant
                        string sourceLabel = GetSourceTypeLabel(directorySourceType);
                        string gameTypeLabel = isZeroHourDir ? "Zero Hour" : "Generals";
                        
                        // EA/Steam format: "Steam - Generals" or "EA App - Zero Hour"
                        gameVersion.Name = $"{sourceLabel} - {gameTypeLabel}";
                        
                        _logger.LogDebug("Found valid retail installation: {Name}, Exec: {Path}", 
                            gameVersion.Name, generalsExePath);
                            
                        result.Add(gameVersion);
                    }
                    else
                    {
                        _logger.LogWarning("No generals.exe found in retail installation directory: {Directory}", directory);
                    }
                    
                    // Return immediately - for retail installations we don't consider other executables
                    return result;
                }
                
                // *** GITHUB ARTIFACT INSTALLATIONS ***
                if (directorySourceType == GameInstallationType.GitHubArtifact)
                {
                    // Get all valid game executables
                    var execFiles = Directory.GetFiles(directory, "*.exe", SearchOption.TopDirectoryOnly)
                        .Where(file => IsValidGameExecutable(file))
                        .ToList();
                    
                    _logger.LogDebug("Found {Count} valid game executables in GitHub directory", execFiles.Count);
                    
                    // Look for specialized GitHub executables first
                    var githubExes = execFiles.Where(file => 
                        Path.GetFileName(file).Equals("generalsv.exe", StringComparison.OrdinalIgnoreCase) ||
                        Path.GetFileName(file).Equals("generalszh.exe", StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    
                    if (githubExes.Any())
                    {
                        foreach (var exePath in githubExes)
                        {
                            string fileName = Path.GetFileName(exePath).ToLowerInvariant();
                            bool isZeroHour = fileName.Equals("generalszh.exe", StringComparison.OrdinalIgnoreCase);
                            
                            var gameVersion = new GameVersion
                            {
                                Id = Guid.NewGuid().ToString(),
                                ExecutablePath = exePath,
                                InstallPath = directory,
                                SourceType = GameInstallationType.GitHubArtifact,
                                IsZeroHour = isZeroHour,
                                GameType = isZeroHour ? "Zero Hour" : "Generals"
                            };
                            
                            // Set specific GitHub version name
                            SetGitHubVersionName(gameVersion);
                            
                            _logger.LogDebug("Added GitHub executable: {Name}, Exec: {Path}",
                                gameVersion.Name, exePath);
                                
                            result.Add(gameVersion);
                        }
                        
                        return result;
                    }
                    
                    // Fallback to generals.exe for GitHub installations
                    var generalsExe = execFiles.FirstOrDefault(f => 
                        Path.GetFileName(f).Equals("generals.exe", StringComparison.OrdinalIgnoreCase));
                    
                    if (generalsExe != null)
                    {
                        // For GitHub installations, we can assume the directory name indicates
                        // whether it's Zero Hour based on workflow naming conventions
                        bool isZeroHourDir = Path.GetFileName(directory).Contains("ZH", StringComparison.OrdinalIgnoreCase) ||
                                            Path.GetFileName(directory).Contains("Zero", StringComparison.OrdinalIgnoreCase);
                        
                        var gameVersion = new GameVersion
                        {
                            Id = Guid.NewGuid().ToString(),
                            ExecutablePath = generalsExe,
                            InstallPath = directory,
                            SourceType = GameInstallationType.GitHubArtifact,
                            IsZeroHour = isZeroHourDir,
                            GameType = isZeroHourDir ? "Zero Hour" : "Generals"
                        };
                        
                        SetGitHubVersionName(gameVersion);
                        result.Add(gameVersion);
                    }
                    
                    return result;
                }
                
                // *** OTHER INSTALLATIONS (LOCAL, CUSTOM, ETC.) ***
                var allValidExes = Directory.GetFiles(directory, "*.exe", SearchOption.TopDirectoryOnly)
                    .Where(IsValidGameExecutable)  // This filters out utility executables
                    .ToList();
                
                if (!allValidExes.Any())
                {
                    _logger.LogWarning("No valid game executables found in directory: {Directory}", directory);
                    return result;
                }
                
                // Process each valid executable
                foreach (var exePath in allValidExes)
                {
                    string fileName = Path.GetFileName(exePath).ToLowerInvariant();
                    
                    // Determine if it's Zero Hour based on executable name
                    bool isZeroHour = fileName.Contains("zh", StringComparison.OrdinalIgnoreCase);
                    
                    var gameVersion = new GameVersion
                    {
                        Id = Guid.NewGuid().ToString(),
                        ExecutablePath = exePath,
                        InstallPath = directory,
                        SourceType = directorySourceType,
                        IsZeroHour = isZeroHour,
                        GameType = isZeroHour ? "Zero Hour" : "Generals"
                    };
                    
                    // Set generic name based on source and game type
                    string sourceLabel = GetSourceTypeLabel(directorySourceType);
                    string gameTypeLabel = isZeroHour ? "Zero Hour" : "Generals";
                    gameVersion.Name = $"{sourceLabel} - {gameTypeLabel}";
                    
                    result.Add(gameVersion);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scanning directory for executables: {Directory}", directory);
                return result;
            }
        }

        /// <summary>
        /// Returns a formatted label for the source type (used in naming)
        /// </summary>
        private string GetSourceTypeLabel(GameInstallationType sourceType)
        {
            return sourceType switch
            {
                GameInstallationType.Steam => "Steam",
                GameInstallationType.EaApp => "EA App",
                GameInstallationType.Origin => "Origin",
                GameInstallationType.TheFirstDecade => "TFD",
                GameInstallationType.GitHubArtifact => "GitHub",
                GameInstallationType.GitHubRelease => "Release",
                GameInstallationType.LocalZipFile => "Local",
                GameInstallationType.DirectoryImport => "Import",
                _ => "Custom"
            };
        }
        
        /// <summary>
        /// Sets a specific name for GitHub builds with workflow information
        /// </summary>
        private void SetGitHubVersionName(GameVersion gameVersion)
        {
            if (gameVersion == null || string.IsNullOrEmpty(gameVersion.InstallPath))
                return;
                
            string gameTypeLabel = gameVersion.IsZeroHour ? "Zero Hour" : "Generals";
            string dirName = Path.GetFileName(gameVersion.InstallPath);
            
            // Parse workflow information from directory name
            // Format example: 20250513_WF1229_Generals-vc6-debug+t+e
            if (dirName.Contains("_WF"))
            {
                var parts = dirName.Split('_');
                if (parts.Length >= 2)
                {
                    string workflowPart = parts.FirstOrDefault(p => p.StartsWith("WF", StringComparison.OrdinalIgnoreCase));
                    if (!string.IsNullOrEmpty(workflowPart))
                    {
                        string wfNumber = workflowPart.Substring(2); // Remove "WF" prefix
                        
                        if (parts.Length >= 3)
                        {
                            // Get build info from third part
                            string buildInfo = parts[2];
                            gameVersion.Name = $"{gameTypeLabel} - WF{wfNumber} - {buildInfo}";
                            return;
                        }
                        
                        gameVersion.Name = $"{gameTypeLabel} - WF{wfNumber}";
                        return;
                    }
                }
            }
            
            // Fallback if no workflow info found
            gameVersion.Name = $"{gameTypeLabel} - GitHub";
        }

        /// <summary>
        /// Determines the source type from installation path with strict rules
        /// </summary>
        private GameInstallationType DetermineSourceType(string directory)
        {
            // Check for Steam with strict path patterns
            if (directory.Contains("steam", StringComparison.OrdinalIgnoreCase) &&
                (directory.Contains("steamapps", StringComparison.OrdinalIgnoreCase) ||
                 directory.Contains("steamlibrary", StringComparison.OrdinalIgnoreCase)))
            {
                return GameInstallationType.Steam;
            }
            
            // Check for EA App/Origin with strict path patterns
            if ((directory.Contains("ea app", StringComparison.OrdinalIgnoreCase) || 
                 directory.Contains("electronic arts", StringComparison.OrdinalIgnoreCase)) &&
                !directory.Contains("genhub", StringComparison.OrdinalIgnoreCase))
            {
                // Look for file markers specific to EA App
                if (File.Exists(Path.Combine(directory, "__Installer", "installerdata.xml")) ||
                    Directory.Exists(Path.Combine(directory, "__Installer")))
                {
                    return GameInstallationType.EaApp;
                }
                
                return GameInstallationType.Origin; // Default to Origin if specific EA App markers aren't found
            }
            
            // Check for The First Decade 
            if (directory.Contains("first decade", StringComparison.OrdinalIgnoreCase) ||
                directory.Contains("tfd", StringComparison.OrdinalIgnoreCase))
            {
                return GameInstallationType.TheFirstDecade;
            }
            
            // Check for GenHub internal paths
            if (directory.Contains("genhub", StringComparison.OrdinalIgnoreCase) &&
                directory.Contains("versions", StringComparison.OrdinalIgnoreCase))
            {
                // GitHub artifacts
                if (directory.Contains("github", StringComparison.OrdinalIgnoreCase))
                {
                    return GameInstallationType.GitHubArtifact;
                }
                
                // Local zip installations
                if (directory.Contains("local", StringComparison.OrdinalIgnoreCase))
                {
                    return GameInstallationType.LocalZipFile;
                }
            }
            
            return GameInstallationType.Unknown;
        }
        
        /// <summary>
        /// Gets information about an executable based on its path
        /// </summary>
        public GameVersion GetExecutableInfo(string executablePath)
        {
            if (string.IsNullOrEmpty(executablePath) || !File.Exists(executablePath) || 
                !IsValidGameExecutable(executablePath))
            {
                _logger.LogWarning("Invalid or non-game executable path: {Path}", executablePath);
                return null;
            }
            
            string directory = Path.GetDirectoryName(executablePath);
            string fileName = Path.GetFileName(executablePath).ToLowerInvariant();
            var sourceType = DetermineSourceType(directory);
            
            // Determine game variant based on installation type
            bool isZeroHour;
            
            if (_retailInstallationTypes.Contains(sourceType))
            {
                // For retail, strictly check directory name
                isZeroHour = IsZeroHourDirectory(directory);
            }
            else
            {
                // For non-retail, check executable name
                isZeroHour = fileName.Contains("zh", StringComparison.OrdinalIgnoreCase);
            }
            
            var gameVersion = new GameVersion
            {
                Id = Guid.NewGuid().ToString(),
                ExecutablePath = executablePath,
                InstallPath = directory,
                SourceType = sourceType,
                IsZeroHour = isZeroHour,
                GameType = isZeroHour ? "Zero Hour" : "Generals"
            };
            
            if (sourceType == GameInstallationType.GitHubArtifact)
            {
                SetGitHubVersionName(gameVersion);
            }
            else
            {
                string sourceLabel = GetSourceTypeLabel(sourceType);
                string gameTypeLabel = isZeroHour ? "Zero Hour" : "Generals";
                gameVersion.Name = $"{sourceLabel} - {gameTypeLabel}";
            }
            
            return gameVersion;
        }

        /// <summary>
        /// Finds the best executable for a given directory, strictly enforcing rules by installation type
        /// </summary>
        public async Task<string> FindBestGameExecutableAsync(
            string directory, 
            bool preferZeroHour = false, 
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            {
                return string.Empty;
            }
            
            try
            {
                // Check JSON metadata first - this is authoritative for GenHub-managed installations
                string dirName = Path.GetFileName(directory);
                string jsonPath = Path.Combine(directory, $"{dirName}.json");
                
                if (File.Exists(jsonPath))
                {
                    try
                    {
                        string json = await File.ReadAllTextAsync(jsonPath, cancellationToken);
                        var version = JsonSerializer.Deserialize<GameVersion>(json);
                        
                        if (version != null && !string.IsNullOrEmpty(version.ExecutablePath) && 
                            File.Exists(version.ExecutablePath) && 
                            IsValidGameExecutable(version.ExecutablePath))
                        {
                            _logger.LogDebug("Using executable from JSON metadata: {ExecutablePath}", version.ExecutablePath);
                            return version.ExecutablePath;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error reading JSON metadata, continuing search");
                    }
                }
            
                // Determine source type for context-aware selection
                var directorySourceType = DetermineSourceType(directory);
                
                // For retail installations, ONLY use generals.exe - no exceptions
                if (_retailInstallationTypes.Contains(directorySourceType))
                {
                    string generalsExePath = Path.Combine(directory, "generals.exe");
                    if (File.Exists(generalsExePath))
                    {
                        _logger.LogDebug("Found generals.exe for retail installation: {Path}", generalsExePath);
                        return generalsExePath;
                    }
                    
                    _logger.LogWarning("No generals.exe found in retail installation directory: {Directory}", directory);
                    return string.Empty; // No fallback for retail installations
                }
                
                // For GitHub installations, use specialized executables
                if (directorySourceType == GameInstallationType.GitHubArtifact)
                {
                    // First check for GitHub-specific executables based on preference
                    if (preferZeroHour)
                    {
                        string zhPath = Path.Combine(directory, "generalszh.exe");
                        if (File.Exists(zhPath))
                        {
                            _logger.LogDebug("Using Zero Hour GitHub executable: {Path}", zhPath);
                            return zhPath;
                        }
                    }
                    else
                    {
                        string vPath = Path.Combine(directory, "generalsv.exe");
                        if (File.Exists(vPath))
                        {
                            _logger.LogDebug("Using Generals GitHub executable: {Path}", vPath);
                            return vPath;
                        }
                    }
                    
                    // Fall back to generals.exe
                    string generalsPath = Path.Combine(directory, "generals.exe");
                    if (File.Exists(generalsPath))
                    {
                        _logger.LogDebug("Using generals.exe fallback for GitHub installation: {Path}", generalsPath);
                        return generalsPath;
                    }
                }
                
                // For other installation types, find all valid executables
                var validExes = Directory.GetFiles(directory, "*.exe", SearchOption.TopDirectoryOnly)
                    .Where(IsValidGameExecutable)
                    .ToList();
                
                if (validExes.Count == 0)
                {
                    _logger.LogWarning("No valid game executables found in directory: {Directory}", directory);
                    return string.Empty;
                }
                
                // First try specific executable matching preference
                if (preferZeroHour)
                {
                    var zhExe = validExes.FirstOrDefault(x => 
                        Path.GetFileName(x).Contains("zh", StringComparison.OrdinalIgnoreCase));
                    
                    if (zhExe != null)
                    {
                        _logger.LogDebug("Selected Zero Hour executable based on preference: {Path}", zhExe);
                        return zhExe;
                    }
                }
                
                // Default to generals.exe if available
                var generalsExe = validExes.FirstOrDefault(x => 
                    Path.GetFileName(x).Equals("generals.exe", StringComparison.OrdinalIgnoreCase));
                
                if (generalsExe != null)
                {
                    _logger.LogDebug("Selected generals.exe as default: {Path}", generalsExe);
                    return generalsExe;
                }
                
                // Last resort: first valid executable
                _logger.LogDebug("Selected first valid executable: {Path}", validExes.First());
                return validExes.First();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding best game executable: {Directory}", directory);
                return string.Empty;
            }
        }

        /// <summary>
        /// Finds a game executable in a directory with simple non-recursive search
        /// </summary>
        public async Task<string> FindExecutableAsync(string directory, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            {
                return string.Empty;
            }
            
            try
            {
                _logger.LogDebug("Finding executable in directory: {Directory}", directory);
                
                // First check for JSON metadata
                string dirName = Path.GetFileName(directory);
                string jsonPath = Path.Combine(directory, $"{dirName}.json");
                
                if (File.Exists(jsonPath))
                {
                    try
                    {
                        string json = await File.ReadAllTextAsync(jsonPath, cancellationToken);
                        var version = JsonSerializer.Deserialize<GameVersion>(json);
                        
                        if (version != null && !string.IsNullOrEmpty(version.ExecutablePath) && 
                            File.Exists(version.ExecutablePath) && IsValidGameExecutable(version.ExecutablePath))
                        {
                            _logger.LogDebug("Using executable from JSON metadata: {Path}", version.ExecutablePath);
                            return version.ExecutablePath;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error reading JSON metadata, continuing search");
                    }
                }
                
                // Determine source type
                var sourceType = DetermineSourceType(directory);
                
                // For retail installations, strictly look for generals.exe
                if (_retailInstallationTypes.Contains(sourceType))
                {
                    string generalsExe = Path.Combine(directory, "generals.exe");
                    if (File.Exists(generalsExe))
                    {
                        _logger.LogDebug("Found generals.exe for retail installation: {Path}", generalsExe);
                        return generalsExe;
                    }
                    
                    _logger.LogWarning("No generals.exe in retail directory: {Directory}", directory);
                    return string.Empty;
                }
                
                // For GitHub installations, look for specific executables first
                if (sourceType == GameInstallationType.GitHubArtifact)
                {
                    string vPath = Path.Combine(directory, "generalsv.exe");
                    string zhPath = Path.Combine(directory, "generalszh.exe");
                    
                    if (File.Exists(vPath))
                    {
                        _logger.LogDebug("Found generalsv.exe for GitHub installation: {Path}", vPath);
                        return vPath;
                    }
                    
                    if (File.Exists(zhPath))
                    {
                        _logger.LogDebug("Found generalszh.exe for GitHub installation: {Path}", zhPath);
                        return zhPath;
                    }
                }
                
                // Look for any valid game executable
                var validExes = Directory.GetFiles(directory, "*.exe", SearchOption.TopDirectoryOnly)
                    .Where(IsValidGameExecutable)
                    .ToList();
                    
                if (validExes.Any())
                {
                    // Prioritize generals.exe
                    var generals = validExes.FirstOrDefault(f => 
                        Path.GetFileName(f).Equals("generals.exe", StringComparison.OrdinalIgnoreCase));
                        
                    if (generals != null)
                    {
                        _logger.LogDebug("Found generals.exe: {Path}", generals);
                        return generals;
                    }
                    
                    // Otherwise return first valid exe
                    _logger.LogDebug("Using first valid executable: {Path}", validExes.First());
                    return validExes.First();
                }
                
                _logger.LogWarning("No valid executables found in directory: {Directory}", directory);
                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding executable in directory: {Directory}", directory);
                return string.Empty;
            }
        }

        /// <summary>
        /// Finds an executable and determines if it was successful
        /// </summary>
        public async Task<(bool Success, string? ExecutablePath)> FindGameExecutableAsync(
            string directory, 
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            {
                return (false, null);
            }
            
            string executablePath = await FindExecutableAsync(directory, cancellationToken);
            bool success = !string.IsNullOrEmpty(executablePath) && File.Exists(executablePath);
            
            return (success, success ? executablePath : null);
        }

        /// <summary>
        /// Helper class to store executable information
        /// </summary>
        private class ExecutableInfo
        {
            public string GameType { get; }
            public bool IsZeroHour { get; }
            public GameInstallationType SourceType { get; }
            public int Priority { get; }
            
            public ExecutableInfo(string gameType, bool isZeroHour, GameInstallationType sourceType, int priority = 0)
            {
                GameType = gameType;
                IsZeroHour = isZeroHour;
                SourceType = sourceType;
                Priority = priority;
            }
        }
    }
}
