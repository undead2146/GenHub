using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Models;
using GenHub.Core.Models.Results;

namespace GenHub.Core.Interfaces.GameVersions
{
    /// <summary>
    /// Orchestrates installation detection + version detection.
    /// </summary>
    public interface IGameVersionDetectionService
    {
        Task<DetectionResult<GameVersion>> DetectAllVersionsAsync(
            CancellationToken cancellationToken = default);

        Task<List<GameVersion>> GetDetectedVersionsAsync(
            CancellationToken cancellationToken = default);
    }
}
