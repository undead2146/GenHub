using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GenHub.Core.Interfaces.Storage
{
    /// <summary>
    /// Interface for JSON-based data persistence operations
    /// </summary>
    public interface IJsonDataService
    {
        /// <summary>
        /// Loads a collection from storage
        /// </summary>
        Task<IEnumerable<T>> LoadCollectionAsync<T>(string fileName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Saves a collection to storage
        /// </summary>
        Task SaveCollectionAsync<T>(string fileName, IEnumerable<T> collection, CancellationToken cancellationToken = default);

        /// <summary>
        /// Loads a category collection
        /// </summary>
        Task<IEnumerable<T>> LoadCategoryAsync<T>(string category, CancellationToken cancellationToken = default);

        /// <summary>
        /// Loads a single item by category and key
        /// </summary>
        Task<T?> LoadAsync<T>(string category, string key, CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Saves a single item
        /// </summary>
        Task SaveAsync<T>(string category, string key, T data, CancellationToken cancellationToken = default);

        /// <summary>
        /// Saves a category collection
        /// </summary>
        Task SaveCategoryAsync<T>(string category, IEnumerable<T> data, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes an item
        /// </summary>
        Task DeleteAsync(string category, string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if an item exists
        /// </summary>
        Task<bool> ExistsAsync(string category, string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Loads a single item from file
        /// </summary>
        Task<T?> LoadItemAsync<T>(string fileName, CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Saves a single item to file
        /// </summary>
        Task SaveItemAsync<T>(string fileName, T item, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if a file exists
        /// </summary>
        bool FileExists(string fileName);

        /// <summary>
        /// Deletes a file
        /// </summary>
        Task DeleteFileAsync(string fileName, CancellationToken cancellationToken = default);
    }
}
