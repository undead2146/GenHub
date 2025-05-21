using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Models.Results;

namespace GenHub.Core.Interfaces.Repositories
{
    /// <summary>
    /// Interface for a generic JSON-based repository that handles entity persistence
    /// </summary>
    /// <typeparam name="TEntity">The entity type</typeparam>
    /// <typeparam name="TKey">The key type for the entity</typeparam>
    public interface IJsonRepository<TEntity, TKey> : IRepository<TEntity, TKey> where TEntity : class, IEntityIdentifier<TKey>
    {
        /// <summary>
        /// Gets the collection name used for storage
        /// </summary>
        string CollectionName { get; }
        
        
        /// <summary>
        /// Saves all entities to storage
        /// </summary>
        Task<OperationResult<IEnumerable<TEntity>>> SaveAllAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Loads all entities from storage
        /// </summary>
        Task<OperationResult<IEnumerable<TEntity>>> LoadAllAsync(CancellationToken cancellationToken = default);
    }
}
