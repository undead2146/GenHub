using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;


namespace GenHub.Core.Interfaces.Repositories
{
    /// <summary>
    /// Generic repository interface defining CRUD operations
    /// Follows the Repository pattern for data access abstraction
    /// </summary>
    /// <typeparam name="TEntity">The entity type that implements IEntityIdentifier</typeparam>
    /// <typeparam name="TKey">The key type for the entity identifier</typeparam>
    public interface IRepository<TEntity, TKey> where TEntity : class, IEntityIdentifier<TKey>
    {
        /// <summary>
        /// Gets an entity by its identifier
        /// </summary>
        /// <param name="id">The entity identifier</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The entity if found, null otherwise</returns>
        Task<TEntity?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all entities from the repository
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Collection of all entities</returns>
        Task<IEnumerable<TEntity>> GetAllAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Adds a new entity to the repository
        /// </summary>
        /// <param name="entity">The entity to add</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The added entity</returns>
        Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Updates an existing entity in the repository
        /// </summary>
        /// <param name="entity">The entity to update</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The updated entity</returns>
        Task<TEntity> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Adds a new entity or updates an existing one (upsert operation)
        /// </summary>
        /// <param name="entity">The entity to add or update</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The added or updated entity</returns>
        Task<TEntity> AddOrUpdateAsync(TEntity entity, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Deletes an entity by its identifier
        /// </summary>
        /// <param name="id">The identifier of the entity to delete</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the entity was deleted successfully, false otherwise</returns>
        Task<bool> DeleteAsync(TKey id, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Checks if an entity with the specified identifier exists
        /// </summary>
        /// <param name="id">The entity identifier to check</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the entity exists, false otherwise</returns>
        Task<bool> ExistsAsync(TKey id, CancellationToken cancellationToken = default);
    }
}
