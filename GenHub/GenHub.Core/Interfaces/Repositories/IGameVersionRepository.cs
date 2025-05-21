using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Models;
using GenHub.Core.Models.GameProfiles;

namespace GenHub.Core.Interfaces.Repositories
{
    /// <summary>
    /// Repository for managing game versions
    /// </summary>
    /// <remarks>
    /// This interface inherits standard CRUD operations from IJsonRepository:
    /// - AddAsync(entity) - Add a new version
    /// - UpdateAsync(entity) - Update an existing version
    /// - DeleteAsync(id) - Delete a version by ID
    /// - GetByIdAsync(id) - Get a version by ID
    /// - GetAllAsync() - Get all versions
    /// - LoadAllAsync() - Load all versions from storage
    /// - SaveAllAsync(entities) - Save a list of versions
    /// </remarks>
    public interface IGameVersionRepository : IJsonRepository<GameVersion, string>
    {
        /// <summary>
        /// Gets the path where game versions are stored
        /// </summary>
        string GetVersionsStoragePath();

        /// <summary>
        /// Gets versions by their source type
        /// </summary>
        Task<IEnumerable<GameVersion>> GetVersionsBySourceTypeAsync(GameInstallationType sourceType, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates a property on a version using reflection (use with caution)
        /// </summary>
        /// <param name="id">ID of the version to update</param>
        /// <param name="propertyName">Name of the property to update</param>
        /// <param name="value">New value for the property</param>
        /// <returns>True if successful, false otherwise</returns>
        Task<bool> UpdateVersionPropertyAsync(string id, string propertyName, object value);
        
    
    }
}
