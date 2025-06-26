using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Models;
using GenHub.Core.Models.Results;

namespace GenHub.Core.Interfaces.GameVersions
{
    /// <summary>
    /// From one or more GameInstallation(s), finds all runnable executables/patches.
    /// </summary>
    public interface IGameVersionDetector
    {
        /// <summary>
        /// Given a set of base installations, produce all GameVersion variants.
        /// </summary>
        Task<DetectionResult<GameVersion>> DetectVersionsFromInstallationsAsync(
           IEnumerable<GameInstallation> installations,
           CancellationToken cancellationToken = default);

        /// <summary>
        /// Scan an arbitrary directory (e.g. a GitHub‐extracted folder) for executables.
        /// </summary>
        Task<DetectionResult<GameVersion>> ScanDirectoryForVersionsAsync(
           string path,
           CancellationToken cancellationToken = default);

        /// <summary>
        /// Validate a single GameVersion (e.g. does the EXE exist?).
        /// </summary>
        Task<bool> ValidateVersionAsync(
           GameVersion version,
           CancellationToken cancellationToken = default);
    }
}
