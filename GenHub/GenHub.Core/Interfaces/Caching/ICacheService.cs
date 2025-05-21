using System;
using System.Threading;
using System.Threading.Tasks;

namespace GenHub.Core.Interfaces.Caching
{
    /// <summary>
    /// Interface for caching services
    /// </summary>
    public interface ICacheService
    {
        /// <summary>
        /// Checks if a cache entry is expired
        /// </summary>
        Task<bool> IsCacheExpiredAsync(string key, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Clears a specific cache entry
        /// </summary>
        Task<bool> ClearCacheAsync(string key, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets data from cache by key
        /// </summary>
        Task<T?> GetFromCacheAsync<T>(string key, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets data from cache if available, otherwise fetches and caches it
        /// </summary>
        Task<T?> GetOrFetchAsync<T>(string key, Func<Task<T>> fetchFunc, TimeSpan? expiration = null, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Saves data to the cache
        /// </summary>
        Task SaveToCacheAsync<T>(string key, T data, TimeSpan? expiration = null, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Saves data to a shared settings file
        /// </summary>
        Task<bool> SaveSharedSettingAsync<T>(string settingName, T data, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets data from a shared settings file
        /// </summary>
        Task<T?> GetSharedSettingAsync<T>(string settingName, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets the shared settings directory path
        /// </summary>
        string GetSharedSettingsDirectory();
        
        /// <summary>
        /// Gets the file path for a cache key in the specified cache directory
        /// </summary>
        string GetCacheFilePath(string cacheDirectory, string key);
        
        /// <summary>
        /// Invalidates a cache entry (legacy method)
        /// </summary>
        void InvalidateCache(string filePath);
        
        /// <summary>
        /// Gets or creates data in cache (legacy method)
        /// </summary>
        Task<T?> GetOrCreateAsync<T>(string filePath, Func<Task<T?>> factory, TimeSpan duration);
        
        /// <summary>
        /// Gets data from cache with a simple key (legacy method)
        /// </summary>
        Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Saves data to cache with a simple key (legacy method)
        /// </summary>
        Task SetAsync<T>(string key, T data, TimeSpan duration, CancellationToken cancellationToken = default);
    }
}
