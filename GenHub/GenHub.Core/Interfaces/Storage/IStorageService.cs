using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GenHub.Core.Interfaces.Storage
{
    /// <summary>
    /// Interface for unified storage service with caching capabilities
    /// </summary>
    public interface IStorageService
    {
        /// <summary>
        /// Gets or fetches data with automatic caching
        /// </summary>
        Task<T?> GetOrFetchAsync<T>(string category, string key, Func<Task<T>> fetchFunc, TimeSpan? cacheDuration = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stores data with automatic caching
        /// </summary>
        Task StoreAsync<T>(string category, string key, T data, TimeSpan? cacheDuration = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets data from storage
        /// </summary>
        Task<T?> GetAsync<T>(string category, string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes data from storage
        /// </summary>
        Task RemoveAsync(string category, string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all items in a category
        /// </summary>
        Task<IEnumerable<T>> GetCategoryAsync<T>(string category, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stores a collection in a category
        /// </summary>
        Task StoreCategoryAsync<T>(string category, IEnumerable<T> items, TimeSpan? cacheDuration = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Clears a category
        /// </summary>
        Task ClearCategoryAsync(string category, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if data exists and is not expired
        /// </summary>
        Task<bool> ExistsAsync(string category, string key, CancellationToken cancellationToken = default);
    }
}
