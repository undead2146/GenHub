using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Models.GameProfiles;
namespace GenHub.Core.Interfaces.Repositories
{
    /// <summary>
    /// Repository for managing game profiles.
    /// Inherits standard CRUD operations from IJsonRepository:
    /// - AddAsync(entity), UpdateAsync(entity), DeleteAsync(id), GetByIdAsync(id), GetAllAsync(), LoadAllAsync(), SaveAllAsync(entities)
    /// </summary>
    public interface IGameProfileRepository : IJsonRepository<GameProfile, string>
    {
        /// <summary>
        /// Gets all custom profiles
        /// </summary>
        Task<IEnumerable<IGameProfile>> LoadCustomProfilesAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Saves all custom profiles
        /// </summary>
        Task SaveCustomProfilesAsync(IEnumerable<IGameProfile> profiles, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets profiles by executable path
        /// </summary>
        Task<IEnumerable<IGameProfile>> GetProfilesByExecutablePathAsync(string executablePath, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Adds or updates a profile in the repository (upsert operation)
        /// </summary>
        Task<bool> AddOrUpdateAsync(IGameProfile profile, CancellationToken cancellationToken = default);
    }
}
