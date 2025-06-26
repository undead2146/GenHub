using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Models;
using GenHub.Core.Models.Results;

namespace GenHub.Core.Interfaces.GameVersions
{
    /// <summary>
    /// Orchestrates version detection for game installations.
    /// </summary>
    public interface IGameVersionDetectionOrchestrator
    {
        Task<DetectionResult<GameVersion>> DetectAllVersionsAsync(
            CancellationToken cancellationToken = default);

        /// <summary>Convenience: return only the Items if Success, else empty.</summary>
        Task<List<GameVersion>> GetDetectedVersionsAsync(
            CancellationToken cancellationToken = default);
    }
}
