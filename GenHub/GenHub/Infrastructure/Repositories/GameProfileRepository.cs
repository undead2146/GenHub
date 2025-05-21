using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using GenHub.Core.Interfaces;
using GenHub.Core.Interfaces.Repositories;
using GenHub.Core.Models.GameProfiles;

namespace GenHub.Infrastructure.Repositories
{
    /// <summary>
    /// Repository for game profile data
    /// </summary>
    public class GameProfileRepository : JsonRepository<GameProfile, string>, IGameProfileRepository
    {
        /// <summary>
        /// Gets the collection name used for storage
        /// </summary>
        public override string CollectionName => "profiles";
        
        /// <summary>
        /// Creates a new instance of GameProfileRepository
        /// </summary>
        public GameProfileRepository(IDataRepository dataRepository, ILogger<GameProfileRepository> logger) 
            : base(dataRepository, logger)
        {
        }
        
        /// <summary>
        /// Gets the ID of a game profile
        /// </summary>
        public override string GetEntityId(GameProfile entity)
        {
            return entity?.Id ?? string.Empty;
        }
        
        /// <summary>
        /// Loads all custom profiles
        /// </summary>
        public async Task<IEnumerable<IGameProfile>> LoadCustomProfilesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await LoadAllAsync(cancellationToken);
                if (!result.Success)
                {
                    _logger.LogWarning("Failed to load profiles: {ErrorMessage}", result.ErrorMessage);
                    return new List<IGameProfile>();
                }
                
                var profiles = result.Data?.Where(p => p.IsCustomProfile).ToList() ?? new List<GameProfile>();
                return profiles.Cast<IGameProfile>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading custom profiles");
                return new List<IGameProfile>();
            }
        }
        
        /// <summary>
        /// Saves all custom profiles
        /// </summary>
        public async Task SaveCustomProfilesAsync(IEnumerable<IGameProfile> profiles, CancellationToken cancellationToken = default)
        {
            try
            {
                // Get all existing profiles
                var result = await LoadAllAsync(cancellationToken);
                var existingProfiles = result.Success
                    ? result.Data?.ToList() ?? new List<GameProfile>()
                    : new List<GameProfile>();
                
                // Remove existing custom profiles
                existingProfiles.RemoveAll(p => p.IsCustomProfile);
                
                // Convert IGameProfile to GameProfile
                var gameProfiles = profiles.Select(p => p is GameProfile gp ? gp : new GameProfile(p)).ToList();
                
                // Add new custom profiles
                existingProfiles.AddRange(gameProfiles);
                
                // Save all profiles
                await SaveAllAsync(existingProfiles, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving custom profiles");
                throw;
            }
        }
        
        /// <summary>
        /// Gets a profile by ID and returns as IGameProfile
        /// </summary>
        public async Task<IGameProfile?> GetProfileByIdAsync(string profileId, CancellationToken cancellationToken = default)
        {
            return await GetByIdAsync(profileId, cancellationToken);
        }
        
        /// <summary>
        /// Gets profiles by executable path
        /// </summary>
        public async Task<IEnumerable<IGameProfile>> GetProfilesByExecutablePathAsync(string executablePath, CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await LoadAllAsync(cancellationToken);
                if (!result.Success)
                {
                    return new List<IGameProfile>();
                }
                
                return result.Data?
                    .Where(p => p.ExecutablePath == executablePath)
                    .Cast<IGameProfile>()
                    .ToList() ?? new List<IGameProfile>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting profiles by executable path");
                return new List<IGameProfile>();
            }
        }
        
        /// <summary>
        /// Deletes a profile
        /// </summary>
        public async Task<bool> DeleteProfileAsync(string profileId, CancellationToken cancellationToken = default)
        {
            return await DeleteAsync(profileId, cancellationToken);
        }
        
        /// <summary>
        /// Gets all profiles as IGameProfile collection
        /// </summary>
        public async Task<IEnumerable<IGameProfile>> GetAllProfilesAsync(CancellationToken cancellationToken = default)
        {
            var result = await GetAllAsync(cancellationToken);
            return result.Cast<IGameProfile>();
        }
        
        /// <summary>
        /// Saves profiles
        /// </summary>
        public async Task SaveProfilesAsync(IEnumerable<IGameProfile> profiles, CancellationToken cancellationToken = default)
        {
            try {
                // Convert IGameProfile to GameProfile
                var gameProfiles = profiles.Select(p => p is GameProfile gp ? gp : new GameProfile(p)).ToList();
                
                // Use base SaveAllAsync method
                await SaveAllAsync(gameProfiles, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving profiles");
                throw;
            }
        }
        
        /// <summary>
        /// Adds or updates a profile in the repository (upsert operation)
        /// </summary>
        public async Task<bool> AddOrUpdateAsync(IGameProfile profile, CancellationToken cancellationToken = default)
        {
            try
            {
                if (profile == null) 
                    throw new ArgumentNullException(nameof(profile));
                
                // Convert to GameProfile if needed
                var gameProfile = profile is GameProfile gp ? gp : new GameProfile(profile);
                
                // Ensure we have an ID (GameProfile should generate one if missing)
                if (string.IsNullOrEmpty(gameProfile.Id))
                {
                    gameProfile.Id = Guid.NewGuid().ToString();
                }
                
                // Check if entity already exists to determine whether to add or update
                var existing = await GetByIdAsync(gameProfile.Id, cancellationToken);
                
                if (existing == null)
                {
                    _logger.LogDebug("Profile {Id} doesn't exist, adding new", gameProfile.Id);
                    await AddAsync(gameProfile, cancellationToken);
                }
                else
                {
                    _logger.LogDebug("Profile {Id} exists, updating", gameProfile.Id);
                    await UpdateAsync(gameProfile, cancellationToken);
                }
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding or updating profile {ProfileId}", profile?.Id);
                return false;
            }
        }
    }
}
