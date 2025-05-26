using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Models;

namespace GenHub.Core.Interfaces
{
    /// <summary>
    /// Interface for platform-specific game detection systems
    /// </summary>
    public interface IGameDetector
    {
        /// <summary>
        /// Asynchronously detects game installations on the current system
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Collection of detected game installations</returns>
        Task<IEnumerable<IGameInstallation>> DetectInstallationsAsync(CancellationToken cancellationToken = default);
    }
}
