using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Interfaces.Caching;
using Microsoft.Extensions.Logging;

namespace GenHub.Infrastructure.Caching
{
    /// <summary>
    /// Service for managing file-based caching of data
    /// </summary>
    public class CachingService : ICacheService
    {
        private readonly ILogger<CachingService> _logger;
        private readonly string _cacheDirectory;
        private readonly string _sharedSettingsDirectory;
        private readonly ConcurrentDictionary<string, object> _memoryCache = new();
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _fileLocks = new();
        private readonly ConcurrentDictionary<string, DateTime> _cacheTimestamps = new();
        private readonly JsonSerializerOptions _serializerOptions;

        /// <summary>
        /// Creates a new instance of CachingService
        /// </summary>
        public CachingService(ILogger<CachingService> logger)
        {
            _logger = logger;
            
            // Set up cache directories
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var basePath = Path.Combine(appDataPath, "GenHub");
            
            _cacheDirectory = Path.Combine(basePath, "cache");
            _sharedSettingsDirectory = Path.Combine(basePath, "settings");
            
            // Create directories if they don't exist
            Directory.CreateDirectory(_cacheDirectory);
            Directory.CreateDirectory(_sharedSettingsDirectory);
            
            // Configure serializer options
            _serializerOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true
            };
            
            _logger.LogInformation("CachingService initialized. Cache directory: {CacheDir}, Settings directory: {SettingsDir}", 
                _cacheDirectory, _sharedSettingsDirectory);
        }

        /// <summary>
        /// Gets a semaphore for synchronizing access to a specific file
        /// </summary>
        private SemaphoreSlim GetFileLock(string key)
        {
            return _fileLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        }

        /// <summary>
        /// Determines if a cache is expired
        /// </summary>
        public async Task<bool> IsCacheExpiredAsync(string key, CancellationToken cancellationToken = default)
        {
            string filePath = GetCacheFilePath(_cacheDirectory, key);
            
            if (!File.Exists(filePath))
                return true;
                
            var metadata = await GetMetadataAsync(key, cancellationToken);
            
            // Check if there's an explicit expiration
            if (metadata.Expiration.HasValue)
            {
                return DateTime.UtcNow > metadata.Expiration.Value;
            }
            
            // Fall back to file timestamp check if no explicit expiration
            if (!_cacheTimestamps.TryGetValue(filePath, out DateTime timestamp))
            {
                timestamp = File.GetLastWriteTimeUtc(filePath);
                _cacheTimestamps[filePath] = timestamp;
            }
            
            // Default behavior: consider 1 day expiration if not specified
            return DateTime.UtcNow - timestamp > TimeSpan.FromDays(1);
        }

        /// <summary>
        /// Saves data to the cache
        /// </summary>
        public async Task SaveToCacheAsync<T>(string key, T data, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Cache key cannot be null or empty", nameof(key));
            }

            try
            {
                // Always update memory cache first
                _memoryCache[key] = data;

                // Prepare cache entry
                var cacheEntry = new CacheEntry<T>
                {
                    Data = data,
                    ExpiresAt = expiration.HasValue ? DateTime.UtcNow.Add(expiration.Value) : null
                };

                // Get file lock
                var fileLock = GetFileLock(key);
                await fileLock.WaitAsync(cancellationToken);

                try
                {
                    // Create the file path
                    string filePath = GetCacheFilePath(_cacheDirectory, key);
                    string json = JsonSerializer.Serialize(cacheEntry, _serializerOptions);

                    // Ensure directory exists 
                    string? directory = Path.GetDirectoryName(filePath);
                    if (!string.IsNullOrEmpty(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    // Use safer file writing approach
                    string tempPath = filePath + ".tmp";
                    await File.WriteAllTextAsync(tempPath, json, Encoding.UTF8, cancellationToken);
                    
                    // If the target file exists, delete it first
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                    }
                    
                    // Rename temp file to actual file
                    File.Move(tempPath, filePath);
                    
                    // Update cache timestamp
                    _cacheTimestamps[filePath] = DateTime.UtcNow;
                    
                    // Save expiration metadata if provided
                    if (expiration.HasValue)
                    {
                        await SaveMetadataAsync(key, new CacheMetadata
                        {
                            LastUpdated = DateTime.UtcNow,
                            Expiration = DateTime.UtcNow + expiration.Value
                        }, cancellationToken);
                    }
                }
                finally
                {
                    fileLock.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving data to cache for key {Key}", key);
                throw;
            }
        }
        
        /// <summary>
        /// Gets data from cache by key
        /// </summary>
        public async Task<T?> GetFromCacheAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                // Check in memory cache first
                if (_memoryCache.TryGetValue(key, out object? cachedValue))
                {
                    return (T?)cachedValue;
                }
                
                // Don't use cancellation token for acquiring the lock to prevent deadlocks
                var fileLock = GetFileLock(key);
                
                try
                {
                    // Wait with a fixed timeout instead of using the cancellation token
                    await fileLock.WaitAsync(TimeSpan.FromSeconds(2));
                    
                    try
                    {
                        string filePath = GetCacheFilePath(_cacheDirectory, key);
                        
                        if (!File.Exists(filePath))
                            return default;
                        
                        // Check expiration
                        if (await IsCacheExpiredAsync(key, CancellationToken.None))
                        {
                            _logger.LogDebug("Cache for {Key} has expired", key);
                            await ClearCacheAsync(key, CancellationToken.None);
                            return default;
                        }
                        
                        // Use cancellation token for reading the file
                        var json = await File.ReadAllTextAsync(filePath, cancellationToken);
                        var cacheEntry = JsonSerializer.Deserialize<CacheEntry<T>>(json, _serializerOptions);
                        
                        if (cacheEntry != null)
                        {
                            // Check expiration in cache entry
                            if (cacheEntry.ExpiresAt.HasValue && cacheEntry.ExpiresAt.Value <= DateTime.UtcNow)
                            {
                                await ClearCacheAsync(key, cancellationToken);
                                return default;
                            }
                            
                            // Update memory cache
                            _memoryCache[key] = cacheEntry.Data;
                            return cacheEntry.Data;
                        }
                        
                        return default;
                    }
                    finally
                    {
                        fileLock.Release();
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("Cache read was canceled for key {Key}", key);
                    return default; // Return null instead of propagating cancellation
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error loading data from cache for key {Key}", key);
                    return default;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading data from cache for key {Key}", key);
                return default;
            }
        }
        
        /// <summary>
        /// Gets data from cache if available, otherwise fetches and caches it
        /// </summary>
        public async Task<T?> GetOrFetchAsync<T>(string key, Func<Task<T>> fetchFunc, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
        {
            // Add a default timeout if none provided
            var effectiveCancellation = cancellationToken;
            var timeoutCtsOwned = false;
            CancellationTokenSource? timeoutCts = null;
            
            try
            {
                if (!cancellationToken.CanBeCanceled)
                {
                    timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                    timeoutCtsOwned = true;
                    effectiveCancellation = timeoutCts.Token;
                }
                
                // Try to get from cache first - BUT USE A SEPARATE TOKEN
                // This avoids cancellation propagating to file operations
                try
                {
                    var cached = await GetFromCacheAsync<T>(key, CancellationToken.None);
                    if (cached != null)
                    {
                        _logger.LogDebug("Cache hit for {Key}", key);
                        return cached;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error reading from cache, will fetch fresh data");
                    // Continue to fetch fresh data on cache read error
                }
                
                // Not in cache or expired, fetch fresh data
                try
                {
                    _logger.LogDebug("Cache miss for {Key}, fetching fresh data", key);
                    
                    // Use the timeout token for the fetch operation
                    var fresh = await fetchFunc();
                    
                    if (fresh != null && !effectiveCancellation.IsCancellationRequested)
                    {
                        // Use a separate token for saving to cache to avoid cancellation issues
                        try
                        {
                            await SaveToCacheAsync(key, fresh, expiration, CancellationToken.None);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error saving to cache, but returning fresh data anyway");
                        }
                    }
                    
                    return fresh;
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Fetch operation was cancelled for key {Key}", key);
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error fetching fresh data for key {Key}", key);
                    return default;
                }
            }
            finally
            {
                // Dispose the timeout CTS if we created it
                if (timeoutCtsOwned)
                {
                    timeoutCts?.Dispose();
                }
            }
        }
        
        /// <summary>
        /// Saves data to a shared settings file that multiple components can access
        /// </summary>
        public async Task<bool> SaveSharedSettingAsync<T>(string settingName, T data, CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                // Sanitize name for file system
                string safeName = string.Join("_", settingName.Split(Path.GetInvalidFileNameChars()));
                string filePath = Path.Combine(_sharedSettingsDirectory, $"{safeName}.json");
                
                // Ensure directory exists
                Directory.CreateDirectory(_sharedSettingsDirectory);
                
                var json = JsonSerializer.Serialize(data, _serializerOptions);
                
                // Use safer file writing with temp file
                string tempPath = filePath + ".tmp";
                await File.WriteAllTextAsync(tempPath, json, cancellationToken);
                
                if (File.Exists(filePath))
                    File.Delete(filePath);
                    
                File.Move(tempPath, filePath);
                
                _logger.LogInformation("Saved shared setting: {SettingName}", settingName);
                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving shared setting {SettingName}", settingName);
                return false;
            }
        }
        
        /// <summary>
        /// Gets data from a shared settings file
        /// </summary>
        public async Task<T?> GetSharedSettingAsync<T>(string settingName, CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                // Sanitize name for file system
                string safeName = string.Join("_", settingName.Split(Path.GetInvalidFileNameChars()));
                string filePath = Path.Combine(_sharedSettingsDirectory, $"{safeName}.json");
                
                if (!File.Exists(filePath))
                    return default;
                
                var json = await File.ReadAllTextAsync(filePath, cancellationToken);
                return JsonSerializer.Deserialize<T>(json, _serializerOptions);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading shared setting {SettingName}", settingName);
                return default;
            }
        }

        /// <summary>
        /// Clears a specific cache entry
        /// </summary>
        public async Task<bool> ClearCacheAsync(string key, CancellationToken cancellationToken = default)
        {
            try
            {
                if (cancellationToken.IsCancellationRequested)
                    return false;
                
                // Get file lock
                var fileLock = GetFileLock(key);
                
                // Use a fixed timeout instead of the cancellation token
                bool lockAcquired = await fileLock.WaitAsync(TimeSpan.FromMilliseconds(500));
                if (!lockAcquired)
                {
                    _logger.LogWarning("Could not acquire lock to clear cache for key {Key}", key);
                    return false;
                }
                
                try
                {
                    string filePath = GetCacheFilePath(_cacheDirectory, key);
                    _memoryCache.TryRemove(key, out _);
                    _cacheTimestamps.TryRemove(filePath, out _);
                    
                    bool success = false;
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                        success = true;
                    }
                    
                    try
                    {
                        // Use a separate method to clear metadata with try/catch
                        await ClearMetadataAsync(key, CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error clearing metadata for {Key}, but cache file was cleared", key);
                    }
                    
                    return success;
                }
                finally
                {
                    fileLock.Release();
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Cache clear operation was canceled for {Key}", key);
                return false; // Return false instead of propagating cancellation
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing cache for key {Key}", key);
                return false;
            }
        }
        
        /// <summary>
        /// Gets the shared settings directory path
        /// </summary>
        public string GetSharedSettingsDirectory()
        {
            return _sharedSettingsDirectory;
        }
        
        /// <summary>
        /// Invalidates a cache entry (legacy method)
        /// </summary>
        public void InvalidateCache(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
                
                string key = Path.GetFileNameWithoutExtension(filePath);
                _memoryCache.TryRemove(key, out _);
                _cacheTimestamps.TryRemove(filePath, out _);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating cache for file {FilePath}", filePath);
            }
        }
        
        /// <summary>
        /// Gets the file path for a cache key in the specified cache directory
        /// </summary>
        public string GetCacheFilePath(string cacheDirectory, string key)
        {
            // Ensure the cache directory exists
            if (!Directory.Exists(cacheDirectory))
            {
                Directory.CreateDirectory(cacheDirectory);
            }
            
            // Sanitize key for file system
            string safeKey = string.Join("_", key.Split(Path.GetInvalidFileNameChars()));
            
            return Path.Combine(cacheDirectory, $"{safeKey}.json");
        }
        
        /// <summary>
        /// Gets or creates data in cache (legacy method)
        /// </summary>
        public async Task<T?> GetOrCreateAsync<T>(string filePath, Func<Task<T?>> factory, TimeSpan duration)
        {
            // Extract key from filepath for compatibility
            string key = Path.GetFileNameWithoutExtension(filePath);
            return await GetOrFetchAsync(key, factory, duration);
        }
        
        /// <summary>
        /// Gets data from cache with a simple key (legacy method)
        /// </summary>
        public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            return await GetFromCacheAsync<T>(key, cancellationToken);
        }
        
        /// <summary>
        /// Saves data to cache with a simple key (legacy method)
        /// </summary>
        public async Task SetAsync<T>(
            string key, 
            T data, 
            TimeSpan duration, 
            CancellationToken cancellationToken = default)
        {
            await SaveToCacheAsync(key, data, duration, cancellationToken);
        }
        
        // Helper methods for metadata
        private string GetMetadataPath(string key)
        {
            string safeKey = string.Join("_", key.Split(Path.GetInvalidFileNameChars()));
            return Path.Combine(_cacheDirectory, $"{safeKey}.metadata.json");
        }
        
        private async Task<CacheMetadata> GetMetadataAsync(string key, CancellationToken cancellationToken = default)
        {
            try
            {
                string path = GetMetadataPath(key);
                if (!File.Exists(path))
                    return new CacheMetadata();
                    
                string json = await File.ReadAllTextAsync(path, cancellationToken);
                return JsonSerializer.Deserialize<CacheMetadata>(json, _serializerOptions) ?? new CacheMetadata();
            }
            catch
            {
                return new CacheMetadata();
            }
        }
        
        private async Task SaveMetadataAsync(string key, CacheMetadata metadata, CancellationToken cancellationToken = default)
        {
            try
            {
                string path = GetMetadataPath(key);
                string json = JsonSerializer.Serialize(metadata, _serializerOptions);
                await File.WriteAllTextAsync(path, json, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving metadata for key {Key}", key);
            }
        }
        
        private async Task ClearMetadataAsync(string key, CancellationToken cancellationToken = default)
        {
            try
            {
                string path = GetMetadataPath(key);
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing metadata for key {Key}", key);
            }
        }
        
        /// <summary>
        /// Metadata for cache entries
        /// </summary>
        private class CacheMetadata
        {
            public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
            public DateTime? Expiration { get; set; }
        }
        
        /// <summary>
        /// Cache entry structure
        /// </summary>
        private class CacheEntry<T>
        {
            public T Data { get; set; } = default!;
            public DateTime? ExpiresAt { get; set; }
        }
    }
}
