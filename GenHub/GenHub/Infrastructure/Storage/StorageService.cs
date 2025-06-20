using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using GenHub.Core.Interfaces.Storage;

namespace GenHub.Infrastructure.Storage
{
    /// <summary>
    /// Storage service with caching capabilities and atomic operations
    /// </summary>
    public class StorageService : IStorageService
    {
        private readonly ILogger<StorageService> _logger;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly string _storageBasePath;

        // Memory cache with expiration tracking
        private readonly ConcurrentDictionary<string, CacheEntry> _memoryCache = new();
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _fileLocks = new();
        
        // Configuration-driven cache durations
        private readonly TimeSpan _defaultCacheDuration;
        private readonly long _maxMemoryCacheSize;
        private readonly Dictionary<string, TimeSpan> _categoryDurations = new();
        private long _currentMemorySize = 0;

        public StorageService(
            ILogger<StorageService> logger,
            JsonSerializerOptions jsonOptions)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _jsonOptions = jsonOptions ?? throw new ArgumentNullException(nameof(jsonOptions));

            // Setup storage paths
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _storageBasePath = Path.Combine(appDataPath, "GenHub");

            // Default configuration values
            _defaultCacheDuration = TimeSpan.FromMinutes(15);
            _maxMemoryCacheSize = 50 * 1024 * 1024; // 50MB

            // Ensure directories exist
            Directory.CreateDirectory(_storageBasePath);
            Directory.CreateDirectory(Path.Combine(_storageBasePath, "Cache"));

            _logger.LogInformation("StorageService initialized. Base path: {BasePath}, Default duration: {Duration}",
                _storageBasePath, _defaultCacheDuration);
        }

        /// <summary>
        /// Gets or fetches data with automatic caching
        /// </summary>
        public async Task<T?> GetOrFetchAsync<T>(string category, string key, Func<Task<T>> fetchFunc, TimeSpan? cacheDuration = null, CancellationToken cancellationToken = default)
        {
            var cacheKey = GetCacheKey(category, key);
            
            // Check memory cache first
            if (_memoryCache.TryGetValue(cacheKey, out var entry) && !entry.IsExpired())
            {
                if (entry.Data is T typedData)
                {
                    _logger.LogDebug("Retrieved {Category}/{Key} from memory cache", category, key);
                    return typedData;
                }
            }

            // Check disk cache
            var diskData = await GetFromDiskAsync<T>(category, key, cancellationToken);
            if (diskData != null)
            {
                // Add to memory cache
                AddToMemoryCache(cacheKey, diskData, cacheDuration ?? GetCacheDuration(category));
                _logger.LogDebug("Retrieved {Category}/{Key} from disk cache", category, key);
                return diskData;
            }

            // Fetch fresh data
            _logger.LogDebug("Fetching fresh data for {Category}/{Key}", category, key);
            var freshData = await fetchFunc();
            
            if (freshData != null)
            {
                await StoreAsync(category, key, freshData, cacheDuration, cancellationToken);
            }

            return freshData;
        }

        /// <summary>
        /// Stores data with automatic caching
        /// </summary>
        public async Task StoreAsync<T>(string category, string key, T data, TimeSpan? cacheDuration = null, CancellationToken cancellationToken = default)
        {
            var cacheKey = GetCacheKey(category, key);
            var duration = cacheDuration ?? GetCacheDuration(category);

            // Add to memory cache
            AddToMemoryCache(cacheKey, data, duration);

            // Save to disk
            await SaveToDiskAsync(category, key, data, duration, cancellationToken);

            _logger.LogDebug("Stored {Category}/{Key} with duration {Duration}", category, key, duration);
        }

        /// <summary>
        /// Gets data from cache/storage
        /// </summary>
        public async Task<T?> GetAsync<T>(string category, string key, CancellationToken cancellationToken = default)
        {
            var cacheKey = GetCacheKey(category, key);
            
            // Check memory cache
            if (_memoryCache.TryGetValue(cacheKey, out var entry) && !entry.IsExpired())
            {
                if (entry.Data is T typedData)
                {
                    return typedData;
                }
            }

            // Check disk cache
            return await GetFromDiskAsync<T>(category, key, cancellationToken);
        }

        /// <summary>
        /// Removes data from cache/storage
        /// </summary>
        public async Task RemoveAsync(string category, string key, CancellationToken cancellationToken = default)
        {
            var cacheKey = GetCacheKey(category, key);
            
            // Remove from memory
            _memoryCache.TryRemove(cacheKey, out _);

            // Remove from disk
            await RemoveFromDiskAsync(category, key, cancellationToken);

            _logger.LogDebug("Removed {Category}/{Key}", category, key);
        }

        /// <summary>
        /// Gets all items in a category
        /// </summary>
        public async Task<IEnumerable<T>> GetCategoryAsync<T>(string category, CancellationToken cancellationToken = default)
        {
            var categoryPath = GetCategoryPath(category);
            if (!Directory.Exists(categoryPath))
                return Enumerable.Empty<T>();

            var items = new List<T>();
            
            try
            {
                var files = Directory.GetFiles(categoryPath, "*.json");
                
                foreach (var file in files)
                {
                    try
                    {
                        var key = Path.GetFileNameWithoutExtension(file);
                        var item = await GetAsync<T>(category, key, cancellationToken);
                        if (item != null)
                            items.Add(item);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error loading item from {File}", file);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading category {Category}", category);
            }

            return items;
        }

        /// <summary>
        /// Stores a collection in a category
        /// </summary>
        public async Task StoreCategoryAsync<T>(string category, IEnumerable<T> items, TimeSpan? cacheDuration = null, CancellationToken cancellationToken = default)
        {
            // Clear existing category
            await ClearCategoryAsync(category, cancellationToken);

            // Store each item
            var itemsList = items.ToList();
            for (int i = 0; i < itemsList.Count; i++)
            {
                var key = $"item_{i:D6}"; // Use index-based keys for collections
                await StoreAsync(category, key, itemsList[i], cacheDuration, cancellationToken);
            }

            _logger.LogInformation("Stored {Count} items in category {Category}", itemsList.Count, category);
        }

        /// <summary>
        /// Clears a category
        /// </summary>
        public async Task ClearCategoryAsync(string category, CancellationToken cancellationToken = default)
        {
            // Clear memory cache entries for this category
            var keysToRemove = _memoryCache.Keys.Where(k => k.StartsWith($"{category}/")).ToList();
            foreach (var key in keysToRemove)
            {
                _memoryCache.TryRemove(key, out _);
            }

            // Clear disk storage
            var categoryPath = GetCategoryPath(category);
            if (Directory.Exists(categoryPath))
            {
                Directory.Delete(categoryPath, true);
                Directory.CreateDirectory(categoryPath);
            }

            _logger.LogDebug("Cleared category {Category}", category);
        }

        /// <summary>
        /// Checks if data exists and is not expired
        /// </summary>
        public async Task<bool> ExistsAsync(string category, string key, CancellationToken cancellationToken = default)
        {
            var cacheKey = GetCacheKey(category, key);
            
            // Check memory cache
            if (_memoryCache.TryGetValue(cacheKey, out var entry) && !entry.IsExpired())
            {
                return true;
            }

            // Check disk cache
            var filePath = GetFilePath(category, key);
            if (!File.Exists(filePath))
                return false;

            // Check if expired
            var metadataPath = filePath + ".meta";
            if (File.Exists(metadataPath))
            {
                try
                {
                    var metadataJson = await File.ReadAllTextAsync(metadataPath, cancellationToken);
                    var metadata = JsonSerializer.Deserialize<CacheMetadata>(metadataJson, _jsonOptions);
                    return metadata?.Expiration > DateTime.UtcNow;
                }
                catch
                {
                    // If metadata is corrupted, consider expired
                    return false;
                }
            }

            return true;
        }

        #region Private Helper Methods

        private TimeSpan GetCacheDuration(string category)
        {
            return _categoryDurations.TryGetValue(category, out var duration) ? duration : _defaultCacheDuration;
        }

        private string GetCacheKey(string category, string key) => $"{category}/{key}";

        private string GetCategoryPath(string category) => Path.Combine(_storageBasePath, "Cache", category);

        private string GetFilePath(string category, string key)
        {
            var categoryPath = GetCategoryPath(category);
            Directory.CreateDirectory(categoryPath);
            return Path.Combine(categoryPath, $"{SanitizeKey(key)}.json");
        }

        private string SanitizeKey(string key)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            foreach (var invalidChar in invalidChars)
            {
                key = key.Replace(invalidChar, '_');
            }
            return key.Replace(' ', '_').Replace(':', '_');
        }

        private void AddToMemoryCache<T>(string cacheKey, T data, TimeSpan duration)
        {
            // Simple memory pressure management
            if (_currentMemorySize > _maxMemoryCacheSize)
            {
                CleanupMemoryCache();
            }

            var entry = new CacheEntry
            {
                Data = data!,
                Expiration = DateTime.UtcNow.Add(duration),
                Size = EstimateSize(data)
            };

            _memoryCache[cacheKey] = entry;
            Interlocked.Add(ref _currentMemorySize, entry.Size);
        }

        private void CleanupMemoryCache()
        {
            var expiredKeys = _memoryCache
                .Where(kvp => kvp.Value.IsExpired())
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                if (_memoryCache.TryRemove(key, out var entry))
                {
                    Interlocked.Add(ref _currentMemorySize, -entry.Size);
                }
            }

            _logger.LogDebug("Cleaned up {Count} expired cache entries", expiredKeys.Count);
        }

        private long EstimateSize<T>(T data)
        {
            return data switch
            {
                string str => str.Length * 2,
                ICollection<object> collection => collection.Count * 100,
                _ => 100
            };
        }

        private async Task<T?> GetFromDiskAsync<T>(string category, string key, CancellationToken cancellationToken)
        {
            var filePath = GetFilePath(category, key);
            
            if (!File.Exists(filePath))
                return default;

            var lockKey = GetCacheKey(category, key);
            var fileLock = _fileLocks.GetOrAdd(lockKey, _ => new SemaphoreSlim(1, 1));
            
            await fileLock.WaitAsync(cancellationToken);
            try
            {
                // Check expiration
                if (!await IsValidCacheAsync(filePath, cancellationToken))
                    return default;

                var json = await File.ReadAllTextAsync(filePath, cancellationToken);
                return JsonSerializer.Deserialize<T>(json, _jsonOptions);
            }
            finally
            {
                fileLock.Release();
            }
        }

        private async Task SaveToDiskAsync<T>(string category, string key, T data, TimeSpan duration, CancellationToken cancellationToken)
        {
            var filePath = GetFilePath(category, key);
            var metadataPath = filePath + ".meta";
            
            var lockKey = GetCacheKey(category, key);
            var fileLock = _fileLocks.GetOrAdd(lockKey, _ => new SemaphoreSlim(1, 1));
            
            await fileLock.WaitAsync(cancellationToken);
            try
            {
                // Save data
                var json = JsonSerializer.Serialize(data, _jsonOptions);
                await File.WriteAllTextAsync(filePath, json, cancellationToken);

                // Save metadata
                var metadata = new CacheMetadata
                {
                    Category = category,
                    Key = key,
                    Expiration = DateTime.UtcNow.Add(duration),
                    CreatedAt = DateTime.UtcNow
                };
                var metadataJson = JsonSerializer.Serialize(metadata, _jsonOptions);
                await File.WriteAllTextAsync(metadataPath, metadataJson, cancellationToken);
            }
            finally
            {
                fileLock.Release();
            }
        }

        private async Task RemoveFromDiskAsync(string category, string key, CancellationToken cancellationToken)
        {
            var filePath = GetFilePath(category, key);
            var metadataPath = filePath + ".meta";

            try
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
                if (File.Exists(metadataPath))
                    File.Delete(metadataPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error removing files for {Category}/{Key}", category, key);
            }
        }

        private async Task<bool> IsValidCacheAsync(string filePath, CancellationToken cancellationToken)
        {
            var metadataPath = filePath + ".meta";
            if (!File.Exists(metadataPath))
                return false;

            try
            {
                var metadataJson = await File.ReadAllTextAsync(metadataPath, cancellationToken);
                var metadata = JsonSerializer.Deserialize<CacheMetadata>(metadataJson, _jsonOptions);
                return metadata?.Expiration > DateTime.UtcNow;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Helper Classes

        private class CacheEntry
        {
            public object Data { get; set; } = null!;
            public DateTime Expiration { get; set; }
            public long Size { get; set; }

            public bool IsExpired() => DateTime.UtcNow > Expiration;
        }

        private class CacheMetadata
        {
            public string Category { get; set; } = string.Empty;
            public string Key { get; set; } = string.Empty;
            public DateTime Expiration { get; set; }
            public DateTime CreatedAt { get; set; }
        }

        #endregion
    }
}
