using System.Threading.Tasks;
using GenHub.Core.Models.GameProfiles;
using GenHub.Core.Models.Results;

namespace GenHub.Core.Interfaces
{
    /// <summary>
    /// Service for launching games
    /// </summary>
    public interface IGameLauncherService
    {
        /// <summary>
        /// Launches a specific game version
        /// </summary>
        Task<OperationResult> LaunchVersionAsync(string versionId, string? arguments = null, bool runAsAdmin = false);
        
        /// <summary>
        /// Launches a game using the specified profile
        /// </summary>
        Task<OperationResult> LaunchVersionAsync(IGameProfile profile);
        
        /// <summary>
        /// Launches a game version directly
        /// </summary>
        Task<OperationResult> LaunchGameVersionAsync(GameVersion version);
        
        /// <summary>
        /// Prepares a game for launching by handling executable path and working directory issues
        /// </summary>
        Task<GameLaunchPrepResult> PrepareGameLaunchAsync(IGameProfile profile);
    }
}
