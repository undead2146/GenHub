using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using GenHub.Core.Interfaces;
using GenHub.Core.Interfaces.Repositories;
using GenHub.Core.Models.GameProfiles;
using GenHub.Core.Models;

namespace GenHub.Features.GameProfiles.Services
{
    /// <summary>
    /// Service for managing game profiles
    /// </summary>
    public class GameProfileManagerService : IGameProfileManagerService
    {
        private readonly IGameProfileRepository _profileRepository;
        private readonly IGameVersionServiceFacade _gameVersionService;
        private readonly IGameProfileFactory _profileFactory;
        private readonly ILogger<GameProfileManagerService> _logger;
        private readonly ProfileMetadataService _profileMetadataService;
        private readonly ProfileResourceService _profileResourceService;

        // Use the event args from the interface
        public event EventHandler<IGameProfileManagerService.ProfilesUpdatedEventArgs> ProfilesUpdated;

        public GameProfileManagerService(
            ILogger<GameProfileManagerService> logger,
            IGameProfileRepository profileRepository,
            IGameVersionServiceFacade gameVersionService,
            ProfileResourceService profileResourceService,
            ProfileMetadataService profileMetadataService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _profileRepository = profileRepository ?? throw new ArgumentNullException(nameof(profileRepository));
            _gameVersionService = gameVersionService ?? throw new ArgumentNullException(nameof(gameVersionService));
            _profileResourceService = profileResourceService ?? throw new ArgumentNullException(nameof(profileResourceService));
            _profileMetadataService = profileMetadataService ?? throw new ArgumentNullException(nameof(profileMetadataService));
        }

        /// <summary>
        /// Gets all game profiles
        /// </summary>
        public async Task<IEnumerable<IGameProfile>> GetProfilesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return await _profileRepository.GetAllAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all profiles");
                return Enumerable.Empty<IGameProfile>();
            }
        }

        /// <summary>
        /// Gets a profile by ID
        /// </summary>
        public async Task<IGameProfile?> GetProfileAsync(string id, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _profileRepository.GetByIdAsync(id, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting profile with ID {Id}", id);
                return null;
            }
        }

        /// <summary>
        /// Gets a profile by executable path
        /// </summary>
        public async Task<IGameProfile?> GetProfileByExecutablePathAsync(string executablePath, CancellationToken cancellationToken = default)
        {
            try
            {
                var profiles = await _profileRepository.GetProfilesByExecutablePathAsync(executablePath, cancellationToken);
                return profiles.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting profile by executable path {Path}", executablePath);
                return null;
            }
        }
        

        /// <summary>
        /// Creates a profile from a version with appropriate resources and metadata
        /// </summary>
        public async Task<IGameProfile> CreateProfileFromVersionAsync(GameVersion version, CancellationToken cancellationToken = default)
        {
            try
            {
                var profile = _profileFactory.CreateFromVersion(version);

                // Extract GitHub info from the version
                if (version.IsFromGitHub)
                {
                    profile.SourceSpecificMetadata = version.GitHubMetadata?.Clone();
                }

                // Use the metadata service to generate a proper description
                profile.Description = _profileMetadataService.GenerateGameDescription(version);

                // Use the resource service to find appropriate icon and cover
                var iconPath = _profileResourceService.FindIconForGameType(version.GameType).Path;
                var coverPath = _profileResourceService.FindCoverForGameType(version.GameType).Path;

                profile.IconPath = iconPath;
                profile.CoverImagePath = coverPath;

                // Save the new profile
                await _profileRepository.AddAsync(profile, cancellationToken);

                return profile;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating profile from version {Version}", version.Id);
                throw;
            }
        }

        /// <summary>
        /// Creates default profiles for installed games
        /// </summary>
        public async Task<IEnumerable<IGameProfile>> CreateDefaultProfilesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Get all installed versions
                var installedVersions = await _gameVersionService.GetInstalledVersionsAsync(cancellationToken);

                // Create profiles for versions that don't already have one
                var profiles = new List<IGameProfile>();

                foreach (var version in installedVersions)
                {
                    // Skip if a profile already exists for this executable
                    var existingProfile = await GetProfileByExecutablePathAsync(version.ExecutablePath, cancellationToken);
                    if (existingProfile != null)
                        continue;

                    // Create a new profile for this version
                    var profile = _profileFactory.CreateFromVersion(version);
                    profile.IsDefaultProfile = true; // Mark as default since it's auto-created

                    // Add to repository
                    await _profileRepository.AddAsync(profile, cancellationToken);

                    profiles.Add(profile);
                }

                return profiles;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating default profiles");
                return Enumerable.Empty<IGameProfile>();
            }
        }

        /// <summary>
        /// Deletes a profile by ID
        /// </summary>
        public async Task DeleteProfileAsync(string id, CancellationToken cancellationToken = default)
        {
            try
            {
                // Don't allow deleting default profiles
                var profile = await _profileRepository.GetByIdAsync(id, cancellationToken);
                if (profile != null && profile.IsDefaultProfile)
                {
                    _logger.LogWarning("Attempted to delete default profile {Id}, operation aborted", id);
                    return;
                }

                await _profileRepository.DeleteAsync(id, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting profile {Id}", id);
                throw;
            }
        }

        /// <summary>
        /// Helper method to safely invoke the ProfilesUpdated event
        /// </summary>
        private void OnProfilesUpdated(object source)
        {
            try
            {
                ProfilesUpdated?.Invoke(this, new IGameProfileManagerService.ProfilesUpdatedEventArgs(source));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ProfilesUpdated event handler");
                // Don't rethrow - we don't want event handler exceptions to crash the app
            }
        }

        /// <summary>
        /// Saves a profile (adds if new or updates if existing)
        /// </summary>
        public async Task SaveProfileAsync(IGameProfile profile, object? source = null, CancellationToken cancellationToken = default)
        {
            try
            {
                if (profile == null)
                {
                    throw new ArgumentNullException(nameof(profile));
                }

                // Check if this is a new profile or an existing one
                var existingProfile = await _profileRepository.GetByIdAsync(profile.Id, cancellationToken);

                if (existingProfile != null)
                {
                    // This is an existing profile - update it while preserving display order
                    await UpdateProfileAsync(profile, source, cancellationToken);
                }
                else
                {
                    // This is a new profile - assign the next display order
                    var allProfiles = await _profileRepository.GetAllAsync(cancellationToken);
                    int maxDisplayOrder = allProfiles.Any()
                        ? allProfiles.Max(p => p.DisplayOrder)
                        : 0;

                    // Create a new profile with the next display order
                    var newProfile = profile is GameProfile gp
                        ? gp
                        : new GameProfile(profile);

                    newProfile.DisplayOrder = maxDisplayOrder + 1;

                    await _profileRepository.AddAsync(newProfile, cancellationToken);

                    // Safely notify listeners that profiles have been updated
                    OnProfilesUpdated(source ?? this);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving profile {Id}", profile.Id);
                throw;
            }
        }

        /// <summary>
        /// Ensures SourceSpecificMetadata is properly initialized for GitHub artifacts
        /// </summary>
        private void EnsureGitHubMetadataInitialized(GameProfile gameProfileToUpdate)
        {
            // Ensure SourceSpecificMetadata is properly initialized for GitHub artifacts
            if (gameProfileToUpdate.SourceType == GameInstallationType.GitHubArtifact)
            {
                if (gameProfileToUpdate.SourceSpecificMetadata == null)
                {
                    // Create new metadata with valid BuildInfo
                    var safeMetadata = new Core.Models.SourceMetadata.GitHubSourceMetadata
                    {
                        BuildInfo = new GitHubBuild
                        {
                            Compiler = "Unknown",
                            Configuration = "Unknown",
                            Version = "Unknown"
                        }
                    };
                    gameProfileToUpdate.SourceSpecificMetadata = safeMetadata;
                }
                else if (gameProfileToUpdate.GitHubMetadata?.BuildInfo == null && gameProfileToUpdate.GitHubMetadata != null)
                {
                    // Ensure BuildInfo exists when GitHubMetadata is not null
                    gameProfileToUpdate.GitHubMetadata.BuildInfo = new GitHubBuild
                    {
                        Compiler = "Unknown",
                        Configuration = "Unknown",
                        Version = "Unknown"
                    };
                }
            }
        }

        /// <summary>
        /// Updates an existing profile
        /// </summary>
        public async Task UpdateProfileAsync(IGameProfile profile, object? source = null, CancellationToken cancellationToken = default)
        {
            try
            {
                if (profile == null)
                {
                    throw new ArgumentNullException(nameof(profile));
                }
                
                // First check if the profile already exists to preserve its display order
                var existingProfile = await _profileRepository.GetByIdAsync(profile.Id, cancellationToken);
                int displayOrder = existingProfile?.DisplayOrder ?? 0;

                // Convert to GameProfile if needed
                GameProfile gameProfileToUpdate;
                if (profile is GameProfile gameProfile)
                {
                    gameProfileToUpdate = gameProfile;
                }
                else
                {
                    gameProfileToUpdate = new GameProfile(profile);
                }

                // Preserve the display order from the existing profile
                if (existingProfile != null && displayOrder > 0)
                {
                    gameProfileToUpdate.DisplayOrder = displayOrder;
                }

                // Use a separate method to handle metadata initialization to avoid direct cast
                EnsureGitHubMetadataInitialized(gameProfileToUpdate);

                // Fix resource paths if necessary
                _profileResourceService.FixResourcePaths(gameProfileToUpdate);
                
                await _profileRepository.UpdateAsync(gameProfileToUpdate, cancellationToken);

                // Use the safe event invocation method
                OnProfilesUpdated(source ?? this);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating profile {Id}", profile.Id);
                throw;
            }
        }


        /// <summary>
        /// Adds a new profile
        /// </summary>
        public async Task AddProfileAsync(IGameProfile profile, object? source = null, CancellationToken cancellationToken = default)
        {
            try
            {
                // Convert to GameProfile if needed
                if (profile is GameProfile gameProfile)
                {
                    await _profileRepository.AddAsync(gameProfile, cancellationToken);
                }
                else
                {
                    var newProfile = new GameProfile(profile);
                    await _profileRepository.AddAsync(newProfile, cancellationToken);
                }

                // Notify listeners that profiles have been updated
                OnProfilesUpdated(source ?? this);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding profile {Id}", profile.Id);
                throw;
            }
        }

        /// <summary>
        /// Saves custom profiles (preserving display order and other UI state)
        /// </summary>
        public async Task SaveCustomProfilesAsync(IEnumerable<IGameProfile> profiles, object? source = null, CancellationToken cancellationToken = default)
        {
            try
            {
                // Convert all profiles to proper GameProfile instances
                var gameProfiles = profiles.Select(p => p is GameProfile gp ? gp : new GameProfile(p)).ToList();

                // Save all profiles at once
                foreach (var profile in gameProfiles)
                {
                    await _profileRepository.UpdateAsync(profile, cancellationToken);
                }

                // Notify listeners that profiles have been updated
                OnProfilesUpdated(source ?? this);

                _logger.LogInformation("Saved {Count} custom profiles", gameProfiles.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving custom profiles");
                throw;
            }
        }

        /// <summary>
        /// Loads custom profiles including their display order and metadata
        /// </summary>
        public async Task<IEnumerable<IGameProfile>> LoadCustomProfilesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Simply return all profiles - the repository already handles loading UI metadata
                var profiles = await _profileRepository.GetAllAsync(cancellationToken);

                // Sort profiles by DisplayOrder field if available
                return profiles.OrderBy(p => p.DisplayOrder);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading custom profiles");
                return Enumerable.Empty<IGameProfile>();
            }
        }
    }
}
