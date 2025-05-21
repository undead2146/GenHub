using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Interfaces;
using GenHub.Core.Models;
using Microsoft.Extensions.Logging;

namespace GenHub.Linux
{
    /// <summary>
    /// Linux-specific implementation of game detection system
    /// </summary>
    public class LinuxGameDetector : IGameDetector
    {
        private readonly ILogger<LinuxGameDetector> _logger;

        /// <summary>
        /// Common Wine/Proton prefixes to search
        /// </summary>
        private readonly string[] _commonPrefixLocations = new[]
        {
            "~/.local/share/Steam/steamapps",
            "~/.steam/steam/steamapps",
            "~/.local/share/lutris",
            "~/.wine",
            "~/.proton"
        };

        /// <summary>
        /// Constructor
        /// </summary>
        public LinuxGameDetector(ILogger<LinuxGameDetector> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Asynchronously detects game installations on Linux
        /// </summary>
        public async Task<IEnumerable<IGameInstallation>> DetectInstallationsAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("LinuxGameDetector: Starting game installation detection");
            var installations = new List<IGameInstallation>();

            try
            {
                // Look for Steam installations
                var steamInstallation = DetectSteamInstallation();
                if (steamInstallation != null && 
                   (steamInstallation.IsVanillaInstalled || steamInstallation.IsZeroHourInstalled))
                {
                    installations.Add(steamInstallation);
                }

                // Look for Lutris installations
                var lutrisInstallation = DetectLutrisInstallation();
                if (lutrisInstallation != null && 
                   (lutrisInstallation.IsVanillaInstalled || lutrisInstallation.IsZeroHourInstalled))
                {
                    installations.Add(lutrisInstallation);
                }

                // Look for plain Wine installations
                var wineInstallation = DetectWineInstallation();
                if (wineInstallation != null && 
                   (wineInstallation.IsVanillaInstalled || wineInstallation.IsZeroHourInstalled))
                {
                    installations.Add(wineInstallation);
                }

                _logger.LogInformation("LinuxGameDetector: Detection complete. Found {Count} installations", installations.Count);
                return installations;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting Linux game installations");
                return installations;
            }
        }

        /// <summary>
        /// Detects Steam installations (using Proton)
        /// </summary>
        private IGameInstallation? DetectSteamInstallation()
        {
            try
            {
                _logger.LogDebug("Searching for Steam installations");
                
                // Expand home directory paths
                var homePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                
                // Common Steam locations on Linux
                var steamPaths = new[]
                {
                    Path.Combine(homePath, ".local", "share", "Steam", "steamapps", "common"),
                    Path.Combine(homePath, ".steam", "steam", "steamapps", "common"),
                    Path.Combine(homePath, ".var", "app", "com.valvesoftware.Steam", "data", "Steam", "steamapps", "common") // Flatpak
                };

                foreach (var steamPath in steamPaths)
                {
                    if (!Directory.Exists(steamPath)) continue;

                    // Check for Generals directory
                    var potentialDirs = Directory.GetDirectories(steamPath)
                        .Where(dir => dir.Contains("Command", StringComparison.OrdinalIgnoreCase) ||
                                     dir.Contains("Conquer", StringComparison.OrdinalIgnoreCase) ||
                                     dir.Contains("Generals", StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    foreach (var dir in potentialDirs)
                    {
                        // Check if it appears to be a game directory
                        if (IsLikelyGameDirectory(dir))
                        {
                            bool isZeroHour = dir.Contains("Zero Hour", StringComparison.OrdinalIgnoreCase) ||
                                              dir.Contains("ZeroHour", StringComparison.OrdinalIgnoreCase);
                                              
                            var installation = new LinuxSteamInstallation();
                            
                            if (isZeroHour)
                            {
                                installation.ZeroHourGamePath = dir;
                                installation.IsZeroHourInstalled = true;
                                
                                // Try to find vanilla directory at same level
                                var parentDir = Directory.GetParent(dir)?.FullName;
                                if (!string.IsNullOrEmpty(parentDir))
                                {
                                    var vanillaDirs = Directory.GetDirectories(parentDir)
                                        .Where(d => d.Contains("Generals", StringComparison.OrdinalIgnoreCase) && 
                                                   !d.Contains("Zero Hour", StringComparison.OrdinalIgnoreCase) &&
                                                   !d.Contains("ZeroHour", StringComparison.OrdinalIgnoreCase) &&
                                                   IsLikelyGameDirectory(d))
                                        .ToList();
                                        
                                    if (vanillaDirs.Any())
                                    {
                                        installation.VanillaGamePath = vanillaDirs.First();
                                        installation.IsVanillaInstalled = true;
                                    }
                                }
                            }
                            else
                            {
                                installation.VanillaGamePath = dir;
                                installation.IsVanillaInstalled = true;
                                
                                // Try to find Zero Hour directory at same level
                                var parentDir = Directory.GetParent(dir)?.FullName;
                                if (!string.IsNullOrEmpty(parentDir))
                                {
                                    var zhDirs = Directory.GetDirectories(parentDir)
                                        .Where(d => (d.Contains("Zero Hour", StringComparison.OrdinalIgnoreCase) || 
                                                   d.Contains("ZeroHour", StringComparison.OrdinalIgnoreCase)) &&
                                                   IsLikelyGameDirectory(d))
                                        .ToList();
                                        
                                    if (zhDirs.Any())
                                    {
                                        installation.ZeroHourGamePath = zhDirs.First();
                                        installation.IsZeroHourInstalled = true;
                                    }
                                }
                            }
                            
                            return installation;
                        }
                    }
                    
                    // Look deeper into compatdata folders for Proton installations
                    var compatPath = Path.Combine(Path.GetDirectoryName(steamPath) ?? string.Empty, "compatdata");
                    if (Directory.Exists(compatPath))
                    {
                        foreach (var pfxDir in Directory.GetDirectories(compatPath))
                        {
                            // Check for the typical Proton drive_c/Program Files structure
                            var programFiles = Path.Combine(pfxDir, "pfx", "drive_c", "Program Files (x86)");
                            if (!Directory.Exists(programFiles))
                            {
                                programFiles = Path.Combine(pfxDir, "pfx", "drive_c", "Program Files");
                            }
                            
                            if (!Directory.Exists(programFiles)) 
                                continue;
                            
                            // Check for EA/Origin directories
                            foreach (var publisher in new[] { "EA Games", "Electronic Arts", "Origin Games" })
                            {
                                var publisherDir = Path.Combine(programFiles, publisher);
                                if (!Directory.Exists(publisherDir)) 
                                    continue;
                                
                                // Look for Generals directories
                                foreach (var dir in Directory.GetDirectories(publisherDir))
                                {
                                    if ((dir.Contains("Generals", StringComparison.OrdinalIgnoreCase) || 
                                         dir.Contains("Command", StringComparison.OrdinalIgnoreCase)) && 
                                        IsLikelyGameDirectory(dir))
                                    {
                                        // Found potential installation - check for Generals and ZH
                                        var installation = new LinuxSteamInstallation();
                                        
                                        // Check if this is a First Decade type installation
                                        string generalsPath = Path.Combine(dir, "Generals");
                                        string zeroHourPath = Path.Combine(dir, "Generals Zero Hour");
                                        
                                        if (Directory.Exists(generalsPath) && IsLikelyGameDirectory(generalsPath))
                                        {
                                            installation.VanillaGamePath = generalsPath;
                                            installation.IsVanillaInstalled = true;
                                        }
                                        else if (dir.Contains("Zero Hour", StringComparison.OrdinalIgnoreCase) ||
                                                IsZeroHourDirectory(dir))
                                        {
                                            installation.ZeroHourGamePath = dir;
                                            installation.IsZeroHourInstalled = true;
                                        }
                                        else
                                        {
                                            installation.VanillaGamePath = dir;
                                            installation.IsVanillaInstalled = true;
                                        }
                                        
                                        if (Directory.Exists(zeroHourPath) && IsLikelyGameDirectory(zeroHourPath))
                                        {
                                            installation.ZeroHourGamePath = zeroHourPath;
                                            installation.IsZeroHourInstalled = true;
                                        }
                                        
                                        if (installation.IsVanillaInstalled || installation.IsZeroHourInstalled)
                                        {
                                            return installation;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting Steam installation on Linux");
            }

            return null;
        }

        /// <summary>
        /// Detects Lutris installations
        /// </summary>
        private IGameInstallation? DetectLutrisInstallation()
        {
            try
            {
                _logger.LogDebug("Searching for Lutris installations");
                
                // Expand home directory
                var homePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var lutrisPath = Path.Combine(homePath, ".local", "share", "lutris");

                if (!Directory.Exists(lutrisPath)) 
                    return null;

                var installation = new LinuxLutrisInstallation();
                bool foundAny = false;
                
                // Lutris stores games in games/ and custom Wine prefixes in runners/wine/
                var gameDirs = new List<string>();

                // Check games directory
                var gamesPath = Path.Combine(lutrisPath, "games");
                if (Directory.Exists(gamesPath))
                {
                    gameDirs.AddRange(Directory.GetDirectories(gamesPath));
                }

                // Check wine prefixes
                var winePath = Path.Combine(lutrisPath, "runners", "wine");
                if (Directory.Exists(winePath))
                {
                    gameDirs.AddRange(Directory.GetDirectories(winePath));
                }

                foreach (var dir in gameDirs)
                {
                    // Lutris games might have "Generals" in the directory name
                    if (dir.Contains("Generals", StringComparison.OrdinalIgnoreCase) ||
                        dir.Contains("Command", StringComparison.OrdinalIgnoreCase) ||
                        dir.Contains("Conquer", StringComparison.OrdinalIgnoreCase))
                    {
                        // Check for actual game directories
                        var potentialPaths = new List<string> { dir };
                        
                        // Check common wine prefix structure
                        var driveCPath = Path.Combine(dir, "drive_c");
                        if (Directory.Exists(driveCPath))
                        {
                            // Get Program Files and Program Files (x86) paths
                            var programPaths = new[] {
                                Path.Combine(driveCPath, "Program Files"),
                                Path.Combine(driveCPath, "Program Files (x86)")
                            }.Where(Directory.Exists);
                            
                            foreach (var programPath in programPaths)
                            {
                                // Look for EA Games, Electronic Arts, etc.
                                foreach (var publisherName in new[] { "EA Games", "Electronic Arts", "Origin Games" })
                                {
                                    var publisherPath = Path.Combine(programPath, publisherName);
                                    if (Directory.Exists(publisherPath))
                                    {
                                        potentialPaths.Add(publisherPath);
                                        
                                        // Add subdirectories containing "Generals" or "Command"
                                        potentialPaths.AddRange(Directory.GetDirectories(publisherPath)
                                            .Where(d => d.Contains("Generals", StringComparison.OrdinalIgnoreCase) || 
                                                      d.Contains("Command", StringComparison.OrdinalIgnoreCase))
                                        );
                                    }
                                }
                            }
                        }
                        
                        // Check all potential paths for game files
                        foreach (var path in potentialPaths)
                        {
                            if (IsLikelyGameDirectory(path))
                            {
                                if (IsZeroHourDirectory(path))
                                {
                                    installation.ZeroHourGamePath = path;
                                    installation.IsZeroHourInstalled = true;
                                    foundAny = true;
                                }
                                else
                                {
                                    installation.VanillaGamePath = path;
                                    installation.IsVanillaInstalled = true;
                                    foundAny = true;
                                }
                            }
                            
                            // Check for First Decade structure with subdirectories
                            var generalsPath = Path.Combine(path, "Generals");
                            var zeroHourPath = Path.Combine(path, "Generals Zero Hour");
                            
                            if (Directory.Exists(generalsPath) && IsLikelyGameDirectory(generalsPath))
                            {
                                installation.VanillaGamePath = generalsPath;
                                installation.IsVanillaInstalled = true;
                                foundAny = true;
                            }
                            
                            if (Directory.Exists(zeroHourPath) && IsLikelyGameDirectory(zeroHourPath))
                            {
                                installation.ZeroHourGamePath = zeroHourPath;
                                installation.IsZeroHourInstalled = true;
                                foundAny = true;
                            }
                        }
                    }
                }
                
                return foundAny ? installation : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting Lutris installation");
            }

            return null;
        }

        /// <summary>
        /// Detects plain Wine installations
        /// </summary>
        private IGameInstallation? DetectWineInstallation()
        {
            try
            {
                _logger.LogDebug("Searching for Wine installations");
                
                // Expand home directory
                var homePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

                // Check default Wine prefix
                var winePath = Path.Combine(homePath, ".wine");
                if (!Directory.Exists(winePath))
                    return null;
                
                // Common locations in Wine prefix
                var programFiles = Path.Combine(winePath, "drive_c", "Program Files");
                var programFilesX86 = Path.Combine(winePath, "drive_c", "Program Files (x86)");

                var installation = new LinuxWineInstallation();
                bool foundAny = false;
                
                // Check EA Games directory in both Program Files locations
                foreach (var baseDir in new[] { programFiles, programFilesX86 })
                {
                    if (!Directory.Exists(baseDir)) continue;

                    // Look for publisher directories
                    var eaDirs = Directory.GetDirectories(baseDir)
                        .Where(dir => dir.Contains("EA Games", StringComparison.OrdinalIgnoreCase) ||
                                    dir.Contains("Electronic Arts", StringComparison.OrdinalIgnoreCase) ||
                                    dir.Contains("Origin Games", StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    foreach (var eaDir in eaDirs)
                    {
                        // Check subdirectories for game installations
                        foreach (var gameDir in Directory.GetDirectories(eaDir))
                        {
                            if (gameDir.Contains("First Decade", StringComparison.OrdinalIgnoreCase) ||
                                gameDir.Contains("Command & Conquer", StringComparison.OrdinalIgnoreCase))
                            {
                                // Look for First Decade structure with Generals and ZH subdirectories
                                var generalsPath = Path.Combine(gameDir, "Generals");
                                var zeroHourPath = Path.Combine(gameDir, "Generals Zero Hour");
                                
                                if (Directory.Exists(generalsPath) && IsLikelyGameDirectory(generalsPath))
                                {
                                    installation.VanillaGamePath = generalsPath;
                                    installation.IsVanillaInstalled = true;
                                    foundAny = true;
                                }
                                
                                if (Directory.Exists(zeroHourPath) && IsLikelyGameDirectory(zeroHourPath))
                                {
                                    installation.ZeroHourGamePath = zeroHourPath;
                                    installation.IsZeroHourInstalled = true;
                                    foundAny = true;
                                }
                            }
                            else if (gameDir.Contains("Generals", StringComparison.OrdinalIgnoreCase))
                            {
                                // Check if this is Zero Hour by path name
                                if (IsZeroHourDirectory(gameDir))
                                {
                                    installation.ZeroHourGamePath = gameDir;
                                    installation.IsZeroHourInstalled = true;
                                    foundAny = true;
                                }
                                else
                                {
                                    installation.VanillaGamePath = gameDir;
                                    installation.IsVanillaInstalled = true;
                                    foundAny = true;
                                    
                                    // Check for Zero Hour subdirectory
                                    var zhSubdir = Directory.GetDirectories(gameDir)
                                        .FirstOrDefault(d => d.Contains("Zero Hour", StringComparison.OrdinalIgnoreCase));
                                        
                                    if (zhSubdir != null && IsLikelyGameDirectory(zhSubdir))
                                    {
                                        installation.ZeroHourGamePath = zhSubdir;
                                        installation.IsZeroHourInstalled = true;
                                    }
                                }
                            }
                        }
                    }
                }
                
                return foundAny ? installation : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting Wine installation");
            }

            return null;
        }
        
        /// <summary>
        /// Determines if a directory contains a Zero Hour installation
        /// </summary>
        private bool IsZeroHourDirectory(string directory)
        {
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                return false;

            try
            {
                // Check path name itself
                if (directory.Contains("zerohour", StringComparison.OrdinalIgnoreCase) ||
                    directory.Contains("zero hour", StringComparison.OrdinalIgnoreCase) ||
                    directory.Contains("zh", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                // Check for ZH-specific files
                if (File.Exists(Path.Combine(directory, "expansion.txt")))
                {
                    return true;
                }
                
                // Check for ZH-specific BIG files
                var bigFiles = Directory.GetFiles(directory, "*.big")
                    .Select(f => Path.GetFileName(f).ToLowerInvariant());
                
                if (bigFiles.Any(f => f.StartsWith("zh") || f.Contains("zerohour")))
                {
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if directory is Zero Hour: {Directory}", directory);
                return false;
            }
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
                string[] commonGameIndicators = {
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
    /// Linux Steam installation (using Proton)
    /// </summary>
    public class LinuxSteamInstallation : IGameInstallation
    {
        public GameInstallationType InstallationType => GameInstallationType.Steam;
        public bool IsVanillaInstalled { get; internal set; }
        public string VanillaGamePath { get; internal set; } = string.Empty;
        public bool IsZeroHourInstalled { get; internal set; }
        public string ZeroHourGamePath { get; internal set; } = string.Empty;

        public void Fetch()
        {
            // Nothing to do, paths are set during detection
        }
    }

    /// <summary>
    /// Linux Lutris installation
    /// </summary>
    public class LinuxLutrisInstallation : IGameInstallation
    {
        public GameInstallationType InstallationType => GameInstallationType.EaApp; // Using EaApp for Lutris
        public bool IsVanillaInstalled { get; internal set; }
        public string VanillaGamePath { get; internal set; } = string.Empty;
        public bool IsZeroHourInstalled { get; internal set; }
        public string ZeroHourGamePath { get; internal set; } = string.Empty;

        public void Fetch()
        {
            // Nothing to do, paths are set during detection
        }
    }

    /// <summary>
    /// Linux Wine installation
    /// </summary>
    public class LinuxWineInstallation : IGameInstallation
    {
        public GameInstallationType InstallationType => GameInstallationType.TheFirstDecade; // Using TFD for Wine
        public bool IsVanillaInstalled { get; internal set; }
        public string VanillaGamePath { get; internal set; } = string.Empty;
        public bool IsZeroHourInstalled { get; internal set; }
        public string ZeroHourGamePath { get; internal set; } = string.Empty;

        public void Fetch()
        {
            // Nothing to do, paths are set during detection
        }
    }
}
