using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Models;
using GenHub.Core.Models.Results;

namespace GenHub.Core.Interfaces.GameInstallations
{
    /// <summary>
    /// Discovers “official” game installations (Steam, EA App, Origin, CD, etc.).
    /// </summary>
    public interface IGameInstallationDetector
    {
        /// <summary>
        /// Human-readable name for logs/UI.
        /// </summary>
        string DetectorName { get; }

        /// <summary>
        /// Only run on the matching OS/platform.
        /// </summary>
        bool CanDetectOnCurrentPlatform { get; }

        /// <summary>
        /// Scan for base platform installations and return them.
        /// </summary>
        Task<DetectionResult<GameInstallation>> DetectInstallationsAsync(
            CancellationToken cancellationToken = default);
    }
}
