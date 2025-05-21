using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Interfaces;
using GenHub.Core.Interfaces.Repositories;
using GenHub.Core.Models;
using GenHub.Core.Models.GameProfiles;
using Microsoft.Extensions.Logging;

namespace GenHub.Infrastructure.Repositories
{
    /// <summary>
    /// Repository implementation for managing game versions
    /// </summary>
    public class GameVersionRepository : JsonRepository<GameVersion, string>, IGameVersionRepository
    {
        private readonly string _versionsPath;
        
        /// <summary>
        /// Gets the collection name used for storage
        /// </summary>
        public override string CollectionName => "versions";

        /// <summary>
        /// Creates a new instance of GameVersionRepository
        /// </summary>
        public GameVersionRepository(IDataRepository dataRepository, ILogger<GameVersionRepository> logger) 
            : base(dataRepository, logger)
        {
            // Create versions directory structure
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _versionsPath = Path.Combine(appDataPath, "GenHub", "Versions");
            
            // Ensure directory exists
            Directory.CreateDirectory(_versionsPath);
            Directory.CreateDirectory(Path.Combine(_versionsPath, "GitHub"));
            Directory.CreateDirectory(Path.Combine(_versionsPath, "Local"));
            
            _logger.LogInformation("GameVersionRepository initialized with path: {Path}", _versionsPath);
        }
        
        /// <summary>
        /// Gets the ID of a game version
        /// </summary>
        public override string GetEntityId(GameVersion entity)
        {
            return entity?.Id ?? string.Empty;
        }
        
        /// <summary>
        /// Gets the path where game versions are stored
        /// </summary>
        public string GetVersionsStoragePath()
        {
            return _versionsPath;
        }

        /// <summary>
        /// Gets versions by their source type
        /// </summary>
        public async Task<IEnumerable<GameVersion>> GetVersionsBySourceTypeAsync(
            GameInstallationType sourceType, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await LoadAllAsync(cancellationToken);
                if (!result.Success)
                {
                    _logger.LogWarning("Failed to load versions: {ErrorMessage}", result.ErrorMessage);
                    return new List<GameVersion>();
                }
                
                return result.Data?.Where(v => v.SourceType == sourceType).ToList() ?? new List<GameVersion>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting versions by source type: {SourceType}", sourceType);
                return new List<GameVersion>();
            }
        }
        
        /// <summary>
        /// Updates a property on a version using reflection (use with caution)
        /// </summary>
        public async Task<bool> UpdateVersionPropertyAsync(string id, string propertyName, object value)
        {
            try
            {
                // Get the version
                var version = await GetByIdAsync(id);
                if (version == null)
                {
                    _logger.LogWarning("Version with ID {VersionId} not found", id);
                    return false;
                }
                
                // Get the property
                var property = typeof(GameVersion).GetProperty(propertyName);
                if (property == null)
                {
                    _logger.LogWarning("Property {PropertyName} not found on GameVersion", propertyName);
                    return false;
                }
                
                // Set the property value
                property.SetValue(version, value);
                
                // Update the version
                await UpdateAsync(version);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating property {PropertyName} on version {VersionId}", propertyName, id);
                return false;
            }
        }

        
    }
}
