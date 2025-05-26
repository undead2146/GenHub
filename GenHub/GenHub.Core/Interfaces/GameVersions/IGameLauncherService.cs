using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Interfaces;
using GenHub.Core.Models;
using GenHub.Core.Models.GameProfiles;
using GenHub.Core.Models.Results;

namespace GenHub.Core.Interfaces.GameVersions
{
    /// <summary>
    /// Service for launching games and game versions
    /// </summary>
    public interface IGameLauncherService
    {
        /// <summary>
        /// Launches a game profile
        /// </summary>
        Task<LaunchResult> LaunchGameAsync(IGameProfile profile, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Launches a game version with optional arguments
        /// </summary>
        Task<LaunchResult> LaunchVersionAsync(GameVersion version, string? arguments = null, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Launches a game profile (alternative signature for compatibility)
        /// </summary>
        Task<LaunchResult> LaunchVersionAsync(IGameProfile profile, CancellationToken cancellationToken = default);
    }
}
