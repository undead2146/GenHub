using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Models.GameProfiles;
using GenHub.Core.Interfaces.GameVersions;

namespace GenHub.Core.Interfaces.Facades
{
    /// <summary>
    /// Facade interface for game detection across different platforms
    /// </summary>
    public interface IGameDetectionFacade : IGameDetector, IGameExecutableLocator
    {
        /// <summary>
        /// Creates GameVersion objects from detected game installations
        /// </summary>
        Task<List<GameVersion>> CreateGameVersionsFromDetectedInstallationsAsync(CancellationToken cancellationToken = default);
    }
}
