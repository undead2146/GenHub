using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Models.GameProfiles;

namespace GenHub.Core.Interfaces
{
    /// <summary>
    /// Interface for discovering and detecting game versions
    /// </summary>
    public interface IGameVersionDiscoveryService
    {
        /// <summary>
        /// Discovers available game versions from all sources
        /// </summary>
        Task<IEnumerable<GameVersion>> DiscoverVersionsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets detected versions without saving them
        /// </summary>
        Task<IEnumerable<GameVersion>> GetDetectedVersionsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets default game versions from standard installation locations
        /// </summary>
        Task<IEnumerable<GameVersion>> GetDefaultGameVersionsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Scans a specific directory for game versions
        /// </summary>
        Task<IEnumerable<GameVersion>> ScanDirectoryForVersionsAsync(string directoryPath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates that a version can be used (exists, has valid executable)
        /// </summary>
        Task<bool> ValidateVersionAsync(GameVersion version, CancellationToken cancellationToken = default);
    }
}
