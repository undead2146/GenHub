using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Interfaces;
using GenHub.Core.Interfaces.Repositories;
using GenHub.Core.Models;
using GenHub.Core.Models.GameProfiles;
using Microsoft.Extensions.Logging;

namespace GenHub.Features.GameVersions.Services
{
    /// <summary>
    /// Implementation of IGameVersionManager for managing game versions
    /// </summary>
    public class GameVersionManager : IGameVersionManager
    {
        private readonly ILogger<GameVersionManager> _logger;
        private readonly IGameVersionRepository _repository;
        private readonly string _versionsPath;

        // Caching support
        private List<GameVersion>? _cachedVersions;
        private DateTime _lastCacheRefresh = DateTime.MinValue;
        private readonly TimeSpan _cacheLifetime = TimeSpan.FromMinutes(5);

        public GameVersionManager(
            ILogger<GameVersionManager> logger,
            IGameVersionRepository repository)
        {
            _logger = logger;
            _repository = repository;
            
            // Set up versions storage path
            _versionsPath = _repository.GetVersionsStoragePath();
            
            // Ensure directories exist
            Directory.CreateDirectory(_versionsPath);
            Directory.CreateDirectory(Path.Combine(_versionsPath, "GitHub"));
            Directory.CreateDirectory(Path.Combine(_versionsPath, "Local"));
            
            _logger.LogInformation("GameVersionManager initialized with path: {Path}", _versionsPath);
        }

        /// <summary>
        /// Gets the path where versions are stored
        /// </summary>
        public string GetVersionsStoragePath()
        {
            return _versionsPath;
        }

        /// <summary>
        /// Gets all installed versions with efficient caching
        /// </summary>
        public async Task<IEnumerable<GameVersion>> GetInstalledVersionsAsync(CancellationToken cancellationToken = default)
        {
            // Return cached versions if still valid
            if (_cachedVersions != null && (DateTime.Now - _lastCacheRefresh) < _cacheLifetime)
            {
                _logger.LogDebug("Returning cached versions list ({Count} items)", _cachedVersions.Count);
                return _cachedVersions;
            }
            
            _logger.LogInformation("Loading installed versions");
            
            try
            {
                // Load versions from repository
                var versions = await _repository.GetAllAsync(cancellationToken);
                
                // Filter for valid versions
                var validVersions = versions.Where(IsValidVersion).ToList();
                
                // Sort versions by preferred order
                validVersions = SortVersionsByPreference(validVersions).ToList();
                
                // Update cache
                _cachedVersions = validVersions;
                _lastCacheRefresh = DateTime.Now;
                
                _logger.LogInformation("Returning {Count} valid installed versions", validVersions.Count);
                return validVersions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting installed versions");
                return new List<GameVersion>();
            }
        }

        /// <summary>
        /// Gets a specific version by ID
        /// </summary>
        public async Task<GameVersion?> GetVersionByIdAsync(string id, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(id))
                return null;
                
            try
            {
                _logger.LogDebug("Getting version by ID: {VersionId}", id);
                
                // First check cache
                if (_cachedVersions != null)
                {
                    var cachedVersion = _cachedVersions.FirstOrDefault(v => v.Id == id);
                    if (cachedVersion != null)
                    {
                        _logger.LogDebug("Found version {VersionId} in cache", id);
                        return cachedVersion;
                    }
                }
                
                // Get from repository
                var version = await _repository.GetByIdAsync(id, cancellationToken);
                
                if (version != null)
                {
                    _logger.LogDebug("Found version {VersionId} in repository", id);
                    
                    // Validate the version
                    if (IsValidVersion(version))
                    {
                        return version;
                    }
                    else
                    {
                        _logger.LogWarning("Version {VersionId} exists but is invalid", id);
                        return null;
                    }
                }
                
                _logger.LogWarning("Version {VersionId} not found", id);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting version by ID: {VersionId}", id);
                return null;
            }
        }

        /// <summary>
        /// Saves a version to the repository
        /// </summary>
        public async Task<bool> SaveVersionAsync(GameVersion version, CancellationToken cancellationToken = default)
        {
            if (version == null) return false;
            
            try
            {
                _logger.LogInformation("Saving version: {VersionId}, {Name}", version.Id, version.Name);
                
                // Check for existing version by executable path
                var existingVersions = await GetInstalledVersionsAsync(cancellationToken);
                var existingVersion = existingVersions.FirstOrDefault(v => 
                    !string.IsNullOrEmpty(v.ExecutablePath) &&
                    !string.IsNullOrEmpty(version.ExecutablePath) &&
                    v.ExecutablePath.Equals(version.ExecutablePath, StringComparison.OrdinalIgnoreCase));
                
                if (existingVersion != null)
                {
                    _logger.LogDebug("Version already exists with executable path: {Path}", version.ExecutablePath);
                    
                    // Update existing version with any new information
                    MergeVersionInfo(existingVersion, version);
                    
                    // Save the updated version
                    await _repository.AddOrUpdateAsync(existingVersion, cancellationToken);
                    
                    // Invalidate cache
                    _cachedVersions = null;
                    
                    return true;
                }
                
                // Save new version
                await _repository.AddOrUpdateAsync(version, cancellationToken);
                
                // Invalidate cache
                _cachedVersions = null;
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving version: {VersionId}", version.Id);
                return false;
            }
        }

        /// <summary>
        /// Updates an existing version
        /// </summary>
        public async Task<bool> UpdateVersionAsync(GameVersion version, CancellationToken cancellationToken = default)
        {
            if (version == null) return false;
            
            try
            {
                _logger.LogInformation("Updating version: {VersionId}, {Name}", version.Id, version.Name);
                
                // Save the version
                await _repository.AddOrUpdateAsync(version, cancellationToken);
                
                // Invalidate cache
                _cachedVersions = null;
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating version: {VersionId}", version.Id);
                return false;
            }
        }

        /// <summary>
        /// Deletes a version by ID
        /// </summary>
        public async Task<bool> DeleteVersionAsync(string id, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(id))
                return false;
                
            try
            {
                _logger.LogInformation("Deleting version: {VersionId}", id);
                
                // Get the version first
                var version = await GetVersionByIdAsync(id, cancellationToken);
                
                if (version == null)
                {
                    _logger.LogWarning("Cannot delete version {VersionId} - not found", id);
                    return false;
                }
                
                // Delete from repository
                await _repository.AddOrUpdateAsync(version, cancellationToken);
                
                // Invalidate cache
                _cachedVersions = null;
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting version: {VersionId}", id);
                return false;
            }
        }

        #region Helper Methods
        /// <summary>
        /// Checks if a version is valid (has existing executable)
        /// </summary>
        private bool IsValidVersion(GameVersion version)
        {
            if (version == null || string.IsNullOrEmpty(version.ExecutablePath))
                return false;
                
            return File.Exists(version.ExecutablePath);
        }
        
        /// <summary>
        /// Sorts versions by preference (GitHub first, then by date, then by name)
        /// </summary>
        private IEnumerable<GameVersion> SortVersionsByPreference(IEnumerable<GameVersion> versions)
        {
            return versions
                .OrderByDescending(v => v.SourceType == GameInstallationType.GitHubArtifact)
                .ThenByDescending(v => v.BuildDate)
                .ThenBy(v => v.Name);
        }

            /// <summary>
            /// Merges information from a source version to a target version.
            /// Only updates fields in target if source has better or more recent information.
            /// </summary>
            private void MergeVersionInfo(GameVersion target, GameVersion source)
            {
                if (target == null || source == null) return;

                // Prefer non-empty, more descriptive names
                if (string.IsNullOrWhiteSpace(target.Name) && !string.IsNullOrWhiteSpace(source.Name))
                target.Name = source.Name;

                // Prefer non-empty descriptions
                if (string.IsNullOrWhiteSpace(target.Description) && !string.IsNullOrWhiteSpace(source.Description))
                target.Description = source.Description;

                // Prefer valid install path
                if (string.IsNullOrWhiteSpace(target.InstallPath) && !string.IsNullOrWhiteSpace(source.InstallPath))
                target.InstallPath = source.InstallPath;

                // Prefer valid game path
                if (string.IsNullOrWhiteSpace(target.GamePath) && !string.IsNullOrWhiteSpace(source.GamePath))
                target.GamePath = source.GamePath;

                // Prefer valid executable path
                if (string.IsNullOrWhiteSpace(target.ExecutablePath) && !string.IsNullOrWhiteSpace(source.ExecutablePath))
                target.ExecutablePath = source.ExecutablePath;

                // Prefer newer install date
                if (source.InstallDate > target.InstallDate)
                target.InstallDate = source.InstallDate;

                // Prefer more specific source type
                if (target.SourceType == GameInstallationType.Unknown && source.SourceType != GameInstallationType.Unknown)
                target.SourceType = source.SourceType;

                // Prefer more specific game type
                if (string.IsNullOrWhiteSpace(target.GameType) && !string.IsNullOrWhiteSpace(source.GameType))
                target.GameType = source.GameType;

                // Prefer source-specific metadata if missing
                if (target.SourceSpecificMetadata == null && source.SourceSpecificMetadata != null)
                target.SourceSpecificMetadata = source.SourceSpecificMetadata;

                // Prefer options if missing
                if (target.Options == null && source.Options != null)
                target.Options = source.Options;

                // Prefer IsValid if source is valid
                if (!target.IsValid && source.IsValid)
                target.IsValid = true;

                // Prefer IsZeroHour if source is true
                if (!target.IsZeroHour && source.IsZeroHour)
                target.IsZeroHour = true;

                // Prefer larger install size
                if (source.InstallSizeBytes > target.InstallSizeBytes)
                target.InstallSizeBytes = source.InstallSizeBytes;

                // Prefer newer build date
                if ((!target.BuildDate.HasValue && source.BuildDate.HasValue) ||
                (source.BuildDate.HasValue && target.BuildDate.HasValue && source.BuildDate > target.BuildDate))
                target.BuildDate = source.BuildDate;
            }
        #endregion
    }
}
