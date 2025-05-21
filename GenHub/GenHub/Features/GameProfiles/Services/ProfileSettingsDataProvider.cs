using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using GenHub.Core.Interfaces;
using GenHub.Core.Models;
using GenHub.Core.Models.GameProfiles;
using GenHub.Features.GameProfiles.ViewModels;

namespace GenHub.Features.GameProfiles.Services
{
    /// <summary>
    /// Default implementation of IProfileSettingsDataProvider
    /// </summary>
    public class ProfileSettingsDataProvider : IProfileSettingsDataProvider
    {
        private readonly ILogger<ProfileSettingsDataProvider> _logger;
        private readonly IGameVersionServiceFacade _versionService;
        private readonly ProfileResourceService _resourceService;
        
        // Cache to improve performance
        private List<GameVersion>? _cachedVersions = null;
        private List<ProfileResourceItem>? _cachedIcons = null;
        private List<ProfileResourceItem>? _cachedCovers = null;
        private List<ExecutablePathItem>? _cachedExecutables = null;
        private List<DataPathItem>? _cachedDataPaths = null;
        
        public ProfileSettingsDataProvider(
            ILogger<ProfileSettingsDataProvider> logger,
            IGameVersionServiceFacade versionService,
            ProfileResourceService resourceService)
        {
            _logger = logger;
            _versionService = versionService;
            _resourceService = resourceService;
        }
        
        /// <summary>
        /// Gets available game versions with caching for improved performance
        /// </summary>
        public async Task<IEnumerable<GameVersion>> GetAvailableVersionsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (_cachedVersions != null && _cachedVersions.Count > 0)
                {
                    _logger.LogDebug("Returning {count} cached versions", _cachedVersions.Count);
                    return _cachedVersions;
                }
                // Check if forced refresh is needed - we may need to refresh the versions list
                _logger.LogDebug("Loading game versions from version service");
                var versions = await _versionService.GetInstalledVersionsAsync(cancellationToken);
                _cachedVersions = versions.ToList();
                return _cachedVersions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available versions");
                return Array.Empty<GameVersion>();
            }
        }

        /// <summary>
        /// Gets available executable paths for game installations
        /// </summary>
        public async Task<IEnumerable<ExecutablePathItem>> GetAvailableExecutablePathsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (_cachedExecutables != null && _cachedExecutables.Count > 0)
                {
                    _logger.LogDebug("Returning {count} cached executable paths", _cachedExecutables.Count);
                    return _cachedExecutables;
                }

                var versions = await GetAvailableVersionsAsync(cancellationToken);
                var executableItems = new List<ExecutablePathItem>();

                foreach (var version in versions)
                {
                    if (!string.IsNullOrEmpty(version.ExecutablePath) && 
                        File.Exists(version.ExecutablePath) &&
                        !executableItems.Any(e => e.Path.Equals(version.ExecutablePath, StringComparison.OrdinalIgnoreCase)))
                    {
                        var gameType = string.IsNullOrEmpty(version.GameType) ? 
                            (version.IsZeroHour ? "Zero Hour" : "Generals") : 
                            version.GameType;
                            
                        var displayName = $"{Path.GetFileName(version.ExecutablePath)} ({gameType}, {version.SourceTypeName})";
                        executableItems.Add(new ExecutablePathItem 
                        { 
                            Path = version.ExecutablePath, 
                            DisplayName = displayName, 
                            GameType = gameType,
                            SourceType = version.SourceType 
                        });
                    }
                }

                _cachedExecutables = executableItems;
                return executableItems;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available executable paths");
                return Array.Empty<ExecutablePathItem>();
            }
        }

        /// <summary>
        /// Gets available data paths for game installations
        /// </summary>
        public async Task<IEnumerable<DataPathItem>> GetAvailableDataPathsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (_cachedDataPaths != null && _cachedDataPaths.Count > 0)
                {
                    _logger.LogDebug("Returning {count} cached data paths", _cachedDataPaths.Count);
                    return _cachedDataPaths;
                }

                var versions = await GetAvailableVersionsAsync(cancellationToken);
                var dataPathItems = new List<DataPathItem>();
                var validInstallationTypes = new HashSet<GameInstallationType>
                {
                    GameInstallationType.Steam,
                    GameInstallationType.EaApp,
                    GameInstallationType.Origin
                };

                foreach (var version in versions)
                {
                    if (!string.IsNullOrEmpty(version.InstallPath) && 
                        Directory.Exists(version.InstallPath) &&
                        !dataPathItems.Any(d => d.Path.Equals(version.InstallPath, StringComparison.OrdinalIgnoreCase)))
                    {
                        var gameType = string.IsNullOrEmpty(version.GameType) ? 
                            (version.IsZeroHour ? "Zero Hour" : "Generals") : 
                            version.GameType;
                            
                        var displayName = $"{Path.GetFileName(version.InstallPath)} ({gameType}, {version.SourceTypeName})";
                        var isValidSource = validInstallationTypes.Contains(version.SourceType);
                        dataPathItems.Add(new DataPathItem(version.InstallPath, displayName, gameType, isValidSource));
                    }
                }

                // Sort by valid source (official installations first)
                var sortedDataPaths = dataPathItems
                    .OrderByDescending(d => d.IsValidSource)
                    .ThenBy(d => d.DisplayName)
                    .ToList();

                _cachedDataPaths = sortedDataPaths;
                return sortedDataPaths;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available data paths");
                return Array.Empty<DataPathItem>();
            }
        }

        /// <summary>
        /// Scans for additional game installations to add to available versions
        /// </summary>
        public async Task<IEnumerable<GameVersion>> ScanForAdditionalVersionsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Scanning for additional game versions");
                
                // Use the DiscoverVersionsAsync method which already does all the scanning work
                return await _versionService.DiscoverVersionsAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scanning for additional versions");
                return Enumerable.Empty<GameVersion>();
            }
        }

        /// <summary>
        /// Formats a user-friendly display name from a version
        /// </summary>
        private string FormatDisplayName(GameVersion version)
        {
            if (version == null)
                return "Unknown Version";
                
            // Check if it's Zero Hour or Generals
            bool isZeroHour = version.IsZeroHour;

            // For GitHub builds, use a specific format
            if (version.GitHubMetadata != null)
            {
                var githubMeta = version.GitHubMetadata;
                
                // If PR number is available, include it
                if (githubMeta.PullRequestNumber.HasValue)
                {
                    return $"{(isZeroHour ? "Zero Hour" : "Generals")} - PR #{githubMeta.PullRequestNumber} Build";
                }

                // If name contains build info, use that
                if (!string.IsNullOrEmpty(version.Name) && 
                    (version.Name.Contains("-vc") || version.Name.Contains("+t") || version.Name.Contains("+e")))
                {
                    return version.Name;
                }

                return $"{(isZeroHour ? "Zero Hour" : "Generals")} - GitHub Build";
            }

            // For regular installations, use version name or format one
            if (!string.IsNullOrEmpty(version.Name))
            {
                return version.Name;
            }

            string source = version.SourceType.ToString();
            return isZeroHour ?
                $"Command & Conquer Generals: Zero Hour ({source})" :
                $"Command & Conquer Generals ({source})";
        }
        
        /// <summary>
        /// Gets available icons with caching for improved performance
        /// </summary>
        public async Task<IEnumerable<ProfileResourceItem>> GetAvailableIconsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (_cachedIcons != null && _cachedIcons.Count > 0)
                {
                    _logger.LogDebug("Returning {count} cached icons", _cachedIcons.Count);
                    return _cachedIcons;
                }
                
                _logger.LogDebug("Loading icons from resource service");
                var icons = await Task.Run(() => _resourceService.GetAvailableIcons(), cancellationToken);
                _cachedIcons = icons;
                return _cachedIcons;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available icons");
                return Array.Empty<ProfileResourceItem>();
            }
        }
        
        /// <summary>
        /// Gets available cover images with caching for improved performance
        /// </summary>
        public async Task<IEnumerable<ProfileResourceItem>> GetAvailableCoverImagesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (_cachedCovers != null && _cachedCovers.Count > 0)
                {
                    _logger.LogDebug("Returning {count} cached covers", _cachedCovers.Count);
                    return _cachedCovers;
                }
                
                _logger.LogDebug("Loading covers from resource service");
                var covers = await Task.Run(() => _resourceService.GetAvailableCovers(), cancellationToken);
                _cachedCovers = covers;
                return _cachedCovers;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available covers");
                return Array.Empty<ProfileResourceItem>();
            }
        }
        
        /// <summary>
        /// Clears all caches to force reload from source
        /// </summary>
        public void ClearCaches()
        {
            _cachedVersions = null;
            _cachedIcons = null;
            _cachedCovers = null;
            _cachedExecutables = null;
            _cachedDataPaths = null;
            _logger.LogInformation("All provider caches cleared");
        }
        
        /// <summary>
        /// Gets icon path for game type
        /// </summary>
        public string GetIconPathForGameType(string gameType)
        {
            try
            {
                var icon = _resourceService.FindIconForGameType(gameType);
                return icon.Path;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting icon path for game type {GameType}", gameType);
                return "";
            }
        }
        
        /// <summary>
        /// Gets cover path for game type
        /// </summary>
        public string GetCoverPathForGameType(string gameType)
        {
            try
            {
                var cover = _resourceService.FindCoverForGameType(gameType);
                return cover.Path;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cover path for game type {GameType}", gameType);
                return "";
            }
        }
        
        /// <summary>
        /// Adds a custom icon and returns its path
        /// </summary>
        public async Task<string> AddCustomIconAsync(string filePath, CancellationToken cancellationToken = default)
        {
            try
            {
                var path = _resourceService.AddIconFromFile(filePath);
                // Clear cache to ensure the new icon is included next time
                _cachedIcons = null;
                return await Task.FromResult(path);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding custom icon from {FilePath}", filePath);
                return "";
            }
        }
        
        /// <summary>
        /// Adds a custom cover and returns its path
        /// </summary>
        public async Task<string> AddCustomCoverAsync(string filePath, CancellationToken cancellationToken = default)
        {
            try
            {
                var path = _resourceService.AddCoverFromFile(filePath);
                // Clear cache to ensure the new cover is included next time
                _cachedCovers = null;
                return await Task.FromResult(path);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding custom cover from {FilePath}", filePath);
                return "";
            }
        }
    }
}
