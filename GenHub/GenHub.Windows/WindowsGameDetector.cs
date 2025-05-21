using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using Microsoft.Extensions.Logging;
using GenHub.Core.Interfaces;
using GenHub.Core.Models;
using GenHub.Windows.Installations;

namespace GenHub.Windows
{
    /// <summary>
    /// Windows-specific implementation of game detection system
    /// </summary>
    public class WindowsGameDetector : IGameDetector
    {
        private readonly ILogger<WindowsGameDetector> _logger;

        /// <summary>
        /// Registry search paths for game installations
        /// </summary>
        private readonly string[] _registrySearchPaths = new[]
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
        };

        public WindowsGameDetector(ILogger<WindowsGameDetector> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Asynchronously detects game installations on the current system
        /// </summary>
        public async Task<IEnumerable<IGameInstallation>> DetectInstallationsAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("WindowsGameDetector: Starting game installation detection");
            var installations = new List<IGameInstallation>();

            try
            {
                // Add standardized installations that do their own detection logic
                AddDefaultInstallations(installations);
                
                // Search for Origin installations
                var originInstallation = DetectOriginInstallation();
                if (originInstallation != null)
                {
                    installations.Add(originInstallation);
                }
                
                // Search for The First Decade installations
                var tfdInstallation = DetectFirstDecadeInstallation();
                if (tfdInstallation != null)
                {
                    installations.Add(tfdInstallation);
                }

                _logger.LogInformation("WindowsGameDetector: Detection complete. Found {Count} installations", installations.Count);
                return installations;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting Windows game installations");
                return installations;
            }
        }

        /// <summary>
        /// Adds default installations to the list
        /// </summary>
        private void AddDefaultInstallations(List<IGameInstallation> installations)
        {
            try
            {
                // Add Steam installation
                var steamInstallation = new SteamInstallation(true);
                if ((steamInstallation.IsVanillaInstalled && !string.IsNullOrEmpty(steamInstallation.VanillaGamePath)) ||
                    (steamInstallation.IsZeroHourInstalled && !string.IsNullOrEmpty(steamInstallation.ZeroHourGamePath)))
                {
                    installations.Add(steamInstallation);
                }
                
                // Add EA App installation
                var eaAppInstallation = new EaAppInstallation(true);
                if ((eaAppInstallation.IsVanillaInstalled && !string.IsNullOrEmpty(eaAppInstallation.VanillaGamePath)) ||
                    (eaAppInstallation.IsZeroHourInstalled && !string.IsNullOrEmpty(eaAppInstallation.ZeroHourGamePath)))
                {
                    installations.Add(eaAppInstallation);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding default installations");
            }
        }
        
        /// <summary>
        /// Detects Origin installation of Generals
        /// </summary>
        private IGameInstallation? DetectOriginInstallation()
        {
            try
            {
                // Check Origin's common installation paths
                string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                string[] originPaths = {
                    Path.Combine(programFiles, "Origin Games"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Origin", "Games"),
                };
                
                foreach (var originPath in originPaths)
                {
                    if (Directory.Exists(originPath))
                    {
                        // Look for Generals or Command & Conquer directories
                        var potentialDirs = Directory.GetDirectories(originPath)
                            .Where(dir => dir.Contains("Command", StringComparison.OrdinalIgnoreCase) ||
                                          dir.Contains("Conquer", StringComparison.OrdinalIgnoreCase) ||
                                          dir.Contains("Generals", StringComparison.OrdinalIgnoreCase))
                            .ToList();
                            
                        foreach (var dir in potentialDirs)
                        {
                            // Check if directory exists and likely contains the game
                            if (IsLikelyGameDirectory(dir))
                            {
                                return new OriginInstallation(dir, _logger);
                            }
                        }
                    }
                }
                
                // Check registry for Origin installations
                foreach (var registryPath in _registrySearchPaths)
                {
                    using var key = Registry.LocalMachine.OpenSubKey(registryPath);
                    if (key == null) continue;
                    
                    foreach (var subkeyName in key.GetSubKeyNames())
                    {
                        using var subkey = key.OpenSubKey(subkeyName);
                        if (subkey == null) continue;
                        
                        var displayName = subkey.GetValue("DisplayName") as string;
                        if (string.IsNullOrEmpty(displayName)) continue;
                        
                        // Check if it's a Generals or Zero Hour installation from Origin
                        if ((displayName.Contains("Generals", StringComparison.OrdinalIgnoreCase) ||
                             displayName.Contains("Zero Hour", StringComparison.OrdinalIgnoreCase)) &&
                            (displayName.Contains("Origin", StringComparison.OrdinalIgnoreCase) ||
                             subkeyName.Contains("Origin", StringComparison.OrdinalIgnoreCase)))
                        {
                            var installPath = subkey.GetValue("InstallLocation") as string;
                            if (!string.IsNullOrEmpty(installPath) && Directory.Exists(installPath))
                            {
                                if (IsLikelyGameDirectory(installPath))
                                {
                                    return new OriginInstallation(installPath, _logger);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting Origin installation");
            }
            
            return null;
        }
        
        /// <summary>
        /// Detects The First Decade installation of Generals
        /// </summary>
        private IGameInstallation? DetectFirstDecadeInstallation()
        {
            try
            {
                // Check common installation paths for The First Decade
                string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                
                string[] tfdPaths = {
                    Path.Combine(programFiles, "EA Games", "Command & Conquer The First Decade"),
                    Path.Combine(programFilesX86, "EA Games", "Command & Conquer The First Decade"),
                    Path.Combine(programFiles, "Electronic Arts", "Command & Conquer The First Decade"),
                    Path.Combine(programFilesX86, "Electronic Arts", "Command & Conquer The First Decade"),
                };
                
                foreach (var tfdPath in tfdPaths)
                {
                    if (Directory.Exists(tfdPath))
                    {
                        // Check for Generals and Zero Hour in The First Decade directory
                        string generalsPath = Path.Combine(tfdPath, "Generals");
                        string zeroHourPath = Path.Combine(tfdPath, "Generals Zero Hour");
                        
                        bool generalsFound = Directory.Exists(generalsPath) && IsLikelyGameDirectory(generalsPath);
                        bool zeroHourFound = Directory.Exists(zeroHourPath) && IsLikelyGameDirectory(zeroHourPath);
                        
                        if (generalsFound || zeroHourFound)
                        {
                            return new FirstDecadeInstallation(
                                generalsFound ? generalsPath : null,
                                zeroHourFound ? zeroHourPath : null
                            );
                        }
                    }
                }
                
                // Check registry for The First Decade
                foreach (var registryPath in _registrySearchPaths)
                {
                    using var key = Registry.LocalMachine.OpenSubKey(registryPath);
                    if (key == null) continue;
                    
                    foreach (var subkeyName in key.GetSubKeyNames())
                    {
                        using var subkey = key.OpenSubKey(subkeyName);
                        if (subkey == null) continue;
                        
                        var displayName = subkey.GetValue("DisplayName") as string;
                        if (string.IsNullOrEmpty(displayName)) continue;
                        
                        if (displayName.Contains("First Decade", StringComparison.OrdinalIgnoreCase) ||
                            displayName.Contains("TFD", StringComparison.OrdinalIgnoreCase))
                        {
                            var installPath = subkey.GetValue("InstallLocation") as string;
                            if (!string.IsNullOrEmpty(installPath) && Directory.Exists(installPath))
                            {
                                // Check for Generals and Zero Hour subdirectories
                                string generalsPath = Path.Combine(installPath, "Generals");
                                string zeroHourPath = Path.Combine(installPath, "Generals Zero Hour");
                                
                                bool generalsFound = Directory.Exists(generalsPath) && IsLikelyGameDirectory(generalsPath);
                                bool zeroHourFound = Directory.Exists(zeroHourPath) && IsLikelyGameDirectory(zeroHourPath);
                                
                                if (generalsFound || zeroHourFound)
                                {
                                    return new FirstDecadeInstallation(
                                        generalsFound ? generalsPath : null,
                                        zeroHourFound ? zeroHourPath : null
                                    );
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting First Decade installation");
            }
            
            return null;
        }

        /// <summary>
        /// Checks if a directory is likely to be a game directory without scanning for executables
        /// </summary>
        private bool IsLikelyGameDirectory(string directory)
        {
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                return false;
                
            try
            {
                // Check for common game files/folders that would indicate this is a game directory
                string[] commonGameFileIndicators = {
                    "Data", // Common folder in C&C games 
                    "Maps", // Maps folder
                    "*.big", // Resource files
                    "*.ini", // Config files
                    "Movies", // Movie folder
                    "Scripts", // Scripts folder
                };
                
                // Check for data directory
                if (Directory.Exists(Path.Combine(directory, "Data")))
                    return true;

                // Check for at least some files that match game patterns
                if (Directory.GetFiles(directory, "*.big").Length > 0)
                    return true;

                // Check for the number of .ini files - game dirs tend to have several
                if (Directory.GetFiles(directory, "*.ini").Length >= 2)
                    return true;
                    
                // Check if the directory name itself indicates a game
                string dirName = Path.GetFileName(directory);
                if (dirName.Contains("Generals", StringComparison.OrdinalIgnoreCase) ||
                    dirName.Contains("Zero Hour", StringComparison.OrdinalIgnoreCase) ||
                    dirName.Contains("ZeroHour", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if directory is a game directory: {Directory}", directory);
                return false;
            }
        }
    }

    /// <summary>
    /// First Decade installation implementation
    /// </summary>
    public class FirstDecadeInstallation : IGameInstallation
    {
        public GameInstallationType InstallationType => GameInstallationType.TheFirstDecade;
        public bool IsVanillaInstalled => !string.IsNullOrEmpty(VanillaGamePath);
        public string VanillaGamePath { get; private set; }
        public bool IsZeroHourInstalled => !string.IsNullOrEmpty(ZeroHourGamePath);
        public string ZeroHourGamePath { get; private set; }

        public FirstDecadeInstallation(string? vanillaPath, string? zeroHourPath)
        {
            VanillaGamePath = vanillaPath ?? string.Empty;
            ZeroHourGamePath = zeroHourPath ?? string.Empty;
        }

        public void Fetch()
        {
            // Already fetched during construction
        }
    }

    /// <summary>
    /// Origin installation implementation
    /// </summary>
    public class OriginInstallation : IGameInstallation
    {
        public GameInstallationType InstallationType => GameInstallationType.Origin;
        public bool IsVanillaInstalled { get; private set; }
        public string VanillaGamePath { get; private set; } = string.Empty;
        public bool IsZeroHourInstalled { get; private set; }
        public string ZeroHourGamePath { get; private set; } = string.Empty;

        private readonly string _basePath;
        private readonly ILogger<WindowsGameDetector> _logger;

        public OriginInstallation(string path, ILogger<WindowsGameDetector> logger)
        {
            _basePath = path;
            _logger = logger;
            Fetch();
        }

        public void Fetch()
        {
            try
            {
                // Origin can install both games in different ways:
                // 1. Both in the same directory (need to distinguish by other means)
                // 2. In separate directories (often with "Zero Hour" in the path)
                
                // Check if this directory contains Zero Hour by looking at path naming
                bool isZeroHourDir = _basePath.Contains("Zero Hour", StringComparison.OrdinalIgnoreCase) ||
                                     _basePath.Contains("ZeroHour", StringComparison.OrdinalIgnoreCase);
                                     
                if (isZeroHourDir)
                {
                    // This is a Zero Hour directory
                    ZeroHourGamePath = _basePath;
                    IsZeroHourInstalled = true;
                    
                    // Check if Generals is in parent directory
                    var parentDir = Directory.GetParent(_basePath)?.FullName;
                    if (!string.IsNullOrEmpty(parentDir))
                    {
                        if (!parentDir.Contains("Zero Hour", StringComparison.OrdinalIgnoreCase))
                        {
                            VanillaGamePath = parentDir;
                            IsVanillaInstalled = true;
                        }
                    }
                }
                else
                {
                    // This is likely a vanilla Generals directory
                    VanillaGamePath = _basePath;
                    IsVanillaInstalled = true;
                    
                    // Look for Zero Hour in subdirectories
                    foreach (var subdir in Directory.GetDirectories(_basePath))
                    {
                        if (subdir.Contains("Zero Hour", StringComparison.OrdinalIgnoreCase) ||
                            subdir.Contains("ZeroHour", StringComparison.OrdinalIgnoreCase))
                        {
                            ZeroHourGamePath = subdir;
                            IsZeroHourInstalled = true;
                            break;
                        }
                    }
                }
                
                // If nothing was found yet, check subdirectories for standard naming patterns
                if (!IsVanillaInstalled && !IsZeroHourInstalled)
                {
                    // Look for Generals and Zero Hour in subdirectories
                    foreach (var subdir in Directory.GetDirectories(_basePath))
                    {
                        if (subdir.Contains("Zero Hour", StringComparison.OrdinalIgnoreCase) ||
                            subdir.Contains("ZeroHour", StringComparison.OrdinalIgnoreCase))
                        {
                            ZeroHourGamePath = subdir;
                            IsZeroHourInstalled = true;
                        }
                        else if (subdir.Contains("Generals", StringComparison.OrdinalIgnoreCase) &&
                                !subdir.Contains("Zero Hour", StringComparison.OrdinalIgnoreCase))
                        {
                            VanillaGamePath = subdir;
                            IsVanillaInstalled = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error fetching Origin installation: {Path}", _basePath);
                IsVanillaInstalled = false;
                IsZeroHourInstalled = false;
            }
        }
    }
}
