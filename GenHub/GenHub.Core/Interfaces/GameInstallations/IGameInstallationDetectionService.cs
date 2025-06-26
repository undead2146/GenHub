using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Models;
using GenHub.Core.Models.Results;

namespace GenHub.Core.Interfaces.GameInstallations
{
    /// <summary>
    /// Orchestrates all IGameInstallationDetector implementations.
    /// </summary>
    public interface IGameInstallationDetectionService
    {
        Task<DetectionResult<GameInstallation>> DetectAllInstallationsAsync(
            CancellationToken cancellationToken = default);

        /// <summary>Convenience: return only the Items if Success, else empty.</summary>
        Task<List<GameInstallation>> GetDetectedInstallationsAsync(
            CancellationToken cancellationToken = default);
    }
}
