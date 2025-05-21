using GenHub.Core.Models.GameProfiles;

namespace GenHub.Core.Interfaces
{
    /// <summary>
    /// Service for managing game profiles
    /// </summary>
    public interface IGameProfileManagerService
    {
        /// <summary>
        /// Event arguments for ProfilesUpdated event
        /// </summary>
        public class ProfilesUpdatedEventArgs : EventArgs
        {
            public object Source { get; }
            public ProfilesUpdatedEventArgs(object source) => Source = source;
        }

        /// <summary>
        /// Event that fires when profiles are updated
        /// </summary>
        event EventHandler<ProfilesUpdatedEventArgs> ProfilesUpdated;

        /// <summary>
        /// Gets all game profiles
        /// </summary>
        Task<IEnumerable<IGameProfile>> GetProfilesAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets a profile by ID
        /// </summary>
        Task<IGameProfile?> GetProfileAsync(string id, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets a profile by executable path
        /// </summary>
        Task<IGameProfile?> GetProfileByExecutablePathAsync(string executablePath, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Creates a new profile from a game version
        /// </summary>
        Task<IGameProfile> CreateProfileFromVersionAsync(GameVersion version, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Creates default profiles for installed games
        /// </summary>
        Task<IEnumerable<IGameProfile>> CreateDefaultProfilesAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Deletes a profile by ID
        /// </summary>
        Task DeleteProfileAsync(string id, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Saves a profile (adds if new or updates if existing)
        /// </summary>
        Task SaveProfileAsync(IGameProfile profile, object source = null, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Adds a new profile
        /// </summary>
        Task AddProfileAsync(IGameProfile profile, object source = null, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Updates an existing profile
        /// </summary>
        Task UpdateProfileAsync(IGameProfile profile, object source = null, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Saves custom profiles (preserving display order and other UI state)
        /// </summary>
        Task SaveCustomProfilesAsync(IEnumerable<IGameProfile> profiles, object source = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Loads custom profiles including their display order and metadata
        /// </summary>
        Task<IEnumerable<IGameProfile>> LoadCustomProfilesAsync(CancellationToken cancellationToken = default);
    }
}
