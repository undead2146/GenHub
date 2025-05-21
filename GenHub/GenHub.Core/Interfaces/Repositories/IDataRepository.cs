using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Models;

namespace GenHub.Core.Interfaces.Repositories
{
    /// <summary>
    /// Interface for accessing cached data used by ViewModels
    /// </summary>
    public interface IDataRepository
    {

        
        /// <summary>
        /// Saves a collection of items to storage
        /// </summary>
        Task SaveCollectionAsync<T>(string collectionName, IEnumerable<T> items);

        /// <summary>
        /// Retrieves a collection of items from storage
        /// </summary>
        Task<IEnumerable<T>?> GetCollectionAsync<T>(string collectionName);

        /// <summary>
        /// Saves a single item to storage
        /// </summary>
        Task SaveItemAsync<T>(string collectionName, string itemId, T item);

        /// <summary>
        /// Deletes an item from storage
        /// </summary>
        Task DeleteItemAsync<T>(string collectionName, string itemId);

        /// <summary>
        /// Retrieves a single item from storage by ID
        /// </summary>
        Task<T?> GetItemByIdAsync<T>(string collectionName, string itemId);
    }
}
