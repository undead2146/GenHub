using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Models.GameProfiles;

namespace GenHub.Core.Interfaces
{
    /// <summary>
    /// Interface for managing game versions (CRUD operations)
    /// </summary>
    public interface IGameVersionManager
    {
        /// <summary>
        /// Gets the path where versions are stored
        /// </summary>
        string GetVersionsStoragePath();

        /// <summary>
        /// Gets all installed versions
        /// </summary>
        Task<IEnumerable<GameVersion>> GetInstalledVersionsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a specific version by ID
        /// </summary>
        Task<GameVersion?> GetVersionByIdAsync(string id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Saves a version to the repository
        /// </summary>
        Task<bool> SaveVersionAsync(GameVersion version, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates an existing version
        /// </summary>
        Task<bool> UpdateVersionAsync(GameVersion version, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a version by ID
        /// </summary>
        Task<bool> DeleteVersionAsync(string id, CancellationToken cancellationToken = default);
    }
}
