using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Models.GameProfiles;

namespace GenHub.Core.Interfaces
{
    /// <summary>
    /// Factory interface for creating and configuring GameProfile instances
    /// </summary>
    public interface IGameProfileFactory
    {
        /// <summary>
        /// Creates a GameProfile from a GameVersion
        /// </summary>
        /// <param name="version">The GameVersion to create a profile from</param>
        /// <returns>A new GameProfile instance</returns>
        GameProfile CreateFromVersion(GameVersion version);
        
        /// <summary>
        /// Populates a GameProfile with metadata from a GameVersion's GitHub artifact
        /// </summary>
        /// <param name="profile">The profile to populate</param>
        /// <param name="version">The version containing GitHub metadata</param>
        void PopulateGitHubMetadata(GameProfile profile, GameVersion version);
        
        /// <summary>
        /// Creates a profile for an executable
        /// </summary>
        /// <param name="executablePath">Path to the executable file</param>
        /// <param name="gameType">Type of game (Generals or Zero Hour)</param>
        /// <returns>A new GameProfile instance</returns>
        Task<GameProfile> CreateFromExecutableAsync(string executablePath, string gameType);
        
        /// <summary>
        /// Creates profiles from a list of executable paths
        /// </summary>
        /// <param name="executablePaths">List of paths to executable files</param>
        /// <param name="existingVersions">Optional list of existing versions to avoid duplicates</param>
        /// <returns>List of created GameProfile instances</returns>
        Task<List<GameProfile>> CreateFromExecutablesAsync(List<string> executablePaths, IEnumerable<GameVersion>? existingVersions = null);
        
        /// <summary>
        /// Normalizes a game type string to ensure consistent naming
        /// </summary>
        /// <param name="gameType">The game type string to normalize</param>
        /// <returns>Normalized game type string</returns>
        string NormalizeGameType(string gameType);
    }
}
