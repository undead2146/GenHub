using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Models;
using GenHub.Core.Models.GameProfiles;

namespace GenHub.Core.Interfaces
{
    /// <summary>
    /// Service for providing data needed by profile settings UI
    /// </summary>
    public interface IProfileSettingsDataProvider
    {
        /// <summary>
        /// Loads available game versions for selection
        /// </summary>
        Task<IEnumerable<GameVersion>> GetAvailableVersionsAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets available data paths for game installations
        /// </summary>
        Task<IEnumerable<DataPathItem>> GetAvailableDataPathsAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets available executable paths for game installations
        /// </summary>
        Task<IEnumerable<ExecutablePathItem>> GetAvailableExecutablePathsAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets available icons for profiles
        /// </summary>
        Task<IEnumerable<ProfileResourceItem>> GetAvailableIconsAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets available cover images for profiles
        /// </summary>
        Task<IEnumerable<ProfileResourceItem>> GetAvailableCoverImagesAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets a valid icon path for the given game type
        /// </summary>
        string GetIconPathForGameType(string gameType);
        
        /// <summary>
        /// Gets a valid cover path for the given game type
        /// </summary>
        string GetCoverPathForGameType(string gameType);
        
        /// <summary>
        /// Adds a custom icon from a file path and returns its resource path
        /// </summary>
        Task<string> AddCustomIconAsync(string filePath, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Adds a custom cover from a file path and returns its resource path
        /// </summary>
        Task<string> AddCustomCoverAsync(string filePath, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Scans for additional game installations to add to available versions
        /// </summary>
        Task<IEnumerable<GameVersion>> ScanForAdditionalVersionsAsync(CancellationToken cancellationToken = default);
    }
}
