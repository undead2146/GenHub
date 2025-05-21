using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;


namespace GenHub.Core.Interfaces.Repositories
{
    /// <summary>
    /// Generic repository interface defining CRUD operations
    /// </summary>
    /// <typeparam name="TEntity">The entity type</typeparam>
    /// <typeparam name="TKey">The key type for the entity</typeparam>
    public interface IRepository<TEntity, TKey> where TEntity : class, IEntityIdentifier<TKey>
    {
        /// <summary>
        /// Gets an entity by its identifier
        /// </summary>
        Task<TEntity?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default);

        
        /// <summary>
        /// Gets all entities
        /// </summary>
        Task<IEnumerable<TEntity>> GetAllAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Adds a new entity
        /// </summary>
        Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Updates an existing entity
        /// </summary>
        Task<TEntity> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Deletes an entity by its identifier
        /// </summary>
        Task<bool> DeleteAsync(TKey id, CancellationToken cancellationToken = default);
        Task<TEntity> AddOrUpdateAsync(TEntity entity, CancellationToken cancellationToken = default);
    }
}
