using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Interfaces.Repositories;
using GenHub.Core.Models.Results;
using Microsoft.Extensions.Logging;

namespace GenHub.Infrastructure.Repositories
{
    /// <summary>
    /// Base class for repositories that store entities as JSON
    /// </summary>
    public abstract class JsonRepository<TEntity, TKey> : IJsonRepository<TEntity, TKey> where TEntity : class, IEntityIdentifier<TKey>
    {
        protected readonly IDataRepository _dataRepository;
        protected readonly ILogger _logger;
        
        public abstract string CollectionName { get; }
        
        /// <summary>
        /// Creates a new instance of JsonRepository
        /// </summary>
        protected JsonRepository(IDataRepository dataRepository, ILogger logger)
        {
            _dataRepository = dataRepository ?? throw new ArgumentNullException(nameof(dataRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        /// <summary>
        /// Retrieves the ID from an entity
        /// </summary>
        public abstract TKey GetEntityId(TEntity entity);

        /// <summary>
        /// Gets an entity by its ID
        /// </summary>
        public virtual async Task<TEntity?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default)
        {
            try
            {
                var entities = await _dataRepository.GetCollectionAsync<TEntity>(CollectionName);
                if (entities == null)
                {
                    return null;
                }
                
                return entities.FirstOrDefault(e => EqualityComparer<TKey>.Default.Equals(GetEntityId(e), id));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting entity with ID {Id} from collection {CollectionName}", id, CollectionName);
                throw;
            }
        }

        /// <summary>
        /// Gets all entities
        /// </summary>
        public virtual async Task<IEnumerable<TEntity>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await _dataRepository.GetCollectionAsync<TEntity>(CollectionName);
                return result ?? new List<TEntity>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all entities from collection {CollectionName}", CollectionName);
                throw;
            }
        }

        /// <summary>
        /// Adds a new entity, fails if the entity already exists
        /// </summary>
        public virtual async Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            
            try
            {
                TKey id = GetEntityId(entity);
                var existing = await GetByIdAsync(id, cancellationToken);
                if (existing != null)
                {
                    throw new InvalidOperationException($"Entity with ID {id} already exists in collection {CollectionName}");
                }
                
                await _dataRepository.SaveItemAsync(CollectionName, id?.ToString() ?? string.Empty, entity);
                return entity;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding entity to collection {CollectionName}", CollectionName);
                throw;
            }
        }

        /// <summary>
        /// Updates an existing entity, adds it if it doesn't exist
        /// </summary>
        public virtual async Task<TEntity> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            
            try
            {
                // Get all entities
                var allEntities = await GetAllAsync(cancellationToken);
                
                // Critical fix: Always initialize a new list if allEntities is null
                List<TEntity> entitiesList;
                if (allEntities == null)
                {
                    entitiesList = new List<TEntity>();
                    _logger.LogWarning("No existing entities found, creating new collection");
                }
                else
                {
                    entitiesList = allEntities.ToList();
                }
                
                // Find and update the entity
                var existingEntityIndex = entitiesList.FindIndex(e => EqualityComparer<TKey>.Default.Equals(GetEntityId(e), GetEntityId(entity)));
                if (existingEntityIndex >= 0)
                {
                    entitiesList[existingEntityIndex] = entity;
                }
                else
                {
                    // Entity not found, add it
                    entitiesList.Add(entity);
                }

                // Save all entities
                await _dataRepository.SaveCollectionAsync(CollectionName, entitiesList);
                return entity;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating entity {Id} in collection {CollectionName}", GetEntityId(entity), CollectionName);
                throw;
            }
        }

        /// <summary>
        /// Adds a new entity or updates it if it already exists
        /// </summary>
        public virtual async Task<TEntity> AddOrUpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            
            try
            {
                TKey id = GetEntityId(entity);
                await _dataRepository.SaveItemAsync(CollectionName, id?.ToString() ?? string.Empty, entity);
                return entity;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding or updating entity in collection {CollectionName}", CollectionName);
                throw;
            }
        }

        /// <summary>
        /// Deletes an entity by its ID
        /// </summary>
        public virtual async Task<bool> DeleteAsync(TKey id, CancellationToken cancellationToken = default)
        {
            try
            {
                await _dataRepository.DeleteItemAsync<TEntity>(CollectionName, id?.ToString() ?? string.Empty);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting entity with ID {Id} from collection {CollectionName}", id, CollectionName);
                return false;
            }
        }

        /// <summary>
        /// Saves all entities
        /// </summary>
        public virtual async Task<OperationResult<IEnumerable<TEntity>>> SaveAllAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
        {
            try
            {
                await _dataRepository.SaveCollectionAsync(CollectionName, entities);
                return OperationResult<IEnumerable<TEntity>>.Succeeded(entities);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving all entities to collection {CollectionName}", CollectionName);
                return OperationResult<IEnumerable<TEntity>>.Failed($"Failed to save {CollectionName}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Loads all entities
        /// </summary>
        public virtual async Task<OperationResult<IEnumerable<TEntity>>> LoadAllAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var entities = await _dataRepository.GetCollectionAsync<TEntity>(CollectionName);
                return OperationResult<IEnumerable<TEntity>>.Succeeded(entities ?? new List<TEntity>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading all entities from collection {CollectionName}", CollectionName);
                return OperationResult<IEnumerable<TEntity>>.Failed($"Failed to load {CollectionName}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Checks if an entity with the specified ID exists
        /// </summary>
        public virtual async Task<bool> ExistsAsync(TKey id, CancellationToken cancellationToken = default)
        {
            try
            {
                var entity = await GetByIdAsync(id, cancellationToken);
                return entity != null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if entity with ID {Id} exists in collection {CollectionName}", id, CollectionName);
                return false; // Return false on error to indicate entity doesn't exist or can't be verified
            }
        }
    }
}
