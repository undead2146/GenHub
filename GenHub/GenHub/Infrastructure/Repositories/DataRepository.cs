using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using GenHub.Core.Interfaces.Repositories;
using GenHub.Features.GameVersions.Json;
using GenHub.Core.Models;
using Microsoft.Extensions.Logging;

namespace GenHub.Infrastructure.Repositories
{
    /// <summary>
    /// Implementation for data repositories
    /// </summary>
    public class DataRepository : IDataRepository
    {
        private readonly string _baseStoragePath;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly ILogger<DataRepository> _logger;
        private readonly Dictionary<string, DateTime> _cacheTimestamps = new Dictionary<string, DateTime>();

        /// <summary>
        /// Creates a new instance of DataRepository
        /// </summary>
        public DataRepository(JsonSerializerOptions jsonOptions, ILogger<DataRepository> logger)
        {
            _jsonOptions = jsonOptions ?? throw new ArgumentNullException(nameof(jsonOptions));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Setup storage path in user's local app data
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _baseStoragePath = Path.Combine(appDataPath, "GenHub", "Data");

            // Ensure directory exists
            Directory.CreateDirectory(_baseStoragePath);
        }

        /// <summary>
        /// Gets the file path for a collection
        /// </summary>
        private string GetFilePath(string collectionName)
        {
            return Path.Combine(_baseStoragePath, $"{collectionName}.json");
        }

        /// <summary>
        /// Determines if a cache is expired
        /// </summary>
        public bool IsCacheExpired(string key, TimeSpan duration)
        {
            if (!_cacheTimestamps.TryGetValue(key, out DateTime timestamp))
            {
                string filePath = GetFilePath(key);
                if (!File.Exists(filePath))
                {
                    return true;
                }

                // Get last write time of the cache file
                timestamp = File.GetLastWriteTimeUtc(filePath);
                _cacheTimestamps[key] = timestamp;
            }

            return DateTime.UtcNow - timestamp > duration;
        }

        /// <summary>
        /// Saves a collection of items
        /// </summary>
        public async Task SaveCollectionAsync<T>(string collectionName, IEnumerable<T> items)
        {
            try
            {
                string filePath = GetFilePath(collectionName);
                var json = JsonSerializer.Serialize(items, _jsonOptions);
                await File.WriteAllTextAsync(filePath, json);

                _logger.LogInformation("Saved collection {CollectionName}", collectionName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving collection {CollectionName}", collectionName);
                throw;
            }
        }

        /// <summary>
        /// Gets a collection of items
        /// </summary>
        public async Task<IEnumerable<T>?> GetCollectionAsync<T>(string collectionName)
        {
            try
            {
                string filePath = GetFilePath(collectionName);

                if (!File.Exists(filePath))
                {
                    return null;
                }

                var json = await File.ReadAllTextAsync(filePath);
                var items = DeserializeJson<IEnumerable<T>>(json);

                _logger.LogInformation("Retrieved collection {CollectionName}", collectionName);
                return items;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting collection {CollectionName}", collectionName);
                throw;
            }
        }

        /// <summary>
        /// Saves a single item. Uses the provided itemId instead of reflection.
        /// </summary>
        public async Task SaveItemAsync<T>(string collectionName, string itemId, T item)
        {
            if (string.IsNullOrEmpty(itemId))
                throw new ArgumentException("Item ID cannot be null or empty", nameof(itemId));

            try
            {
                // Get existing collection
                var collection = await GetCollectionAsync<T>(collectionName) ?? new List<T>();
                var collectionList = new List<T>(collection);

                // Try to find existing item using IEntityIdentifier when possible
                bool found = false;
                for (int i = 0; i < collectionList.Count; i++)
                {
                    // Try for custom identifier interfaces
                    if (collectionList[i] is IEntityIdentifier<string> stringIdentifiable &&
                        stringIdentifiable.Id == itemId)
                    {
                        collectionList[i] = item;
                        found = true;
                        break;
                    }

                    // Fall back to reflection only if necessary
                    var existingItem = collectionList[i];
                    var idProperty = existingItem?.GetType().GetProperty("Id");
                    if (idProperty != null)
                    {
                        var existingId = idProperty.GetValue(existingItem)?.ToString();
                        if (existingId == itemId)
                        {
                            // Replace existing item
                            collectionList[i] = item;
                            found = true;
                            break;
                        }
                    }
                }

                // Add new item if not found
                if (!found)
                {
                    collectionList.Add(item);
                }

                // Save updated collection
                await SaveCollectionAsync(collectionName, collectionList);

                _logger.LogInformation("Saved item {ItemId} in collection {CollectionName}", itemId, collectionName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving item {ItemId} in collection {CollectionName}", itemId, collectionName);
                throw;
            }
        }

        /// <summary>
        /// Deletes an item
        /// </summary>
        public async Task DeleteItemAsync<T>(string collectionName, string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
                throw new ArgumentException("Item ID cannot be null or empty", nameof(itemId));

            try
            {
                // Get existing collection
                var collection = await GetCollectionAsync<T>(collectionName);
                if (collection == null)
                {
                    return;
                }

                var collectionList = new List<T>(collection);

                // Find and remove item
                bool found = false;
                for (int i = collectionList.Count - 1; i >= 0; i--)
                {
                    // Try for custom identifier interfaces
                    if (collectionList[i] is IEntityIdentifier<string> stringIdentifiable &&
                        stringIdentifiable.Id == itemId)
                    {
                        collectionList.RemoveAt(i);
                        found = true;
                        break;
                    }

                    // Fall back to reflection only if necessary
                    var existingItem = collectionList[i];
                    var idProperty = existingItem?.GetType().GetProperty("Id");
                    if (idProperty != null)
                    {
                        var existingId = idProperty.GetValue(existingItem)?.ToString();
                        if (existingId == itemId)
                        {
                            collectionList.RemoveAt(i);
                            found = true;
                            break;
                        }
                    }
                }

                if (found)
                {
                    // Save updated collection
                    await SaveCollectionAsync(collectionName, collectionList);
                    _logger.LogInformation("Deleted item {ItemId} from collection {CollectionName}", itemId, collectionName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting item {ItemId} from collection {CollectionName}", itemId, collectionName);
                throw;
            }
        }

        /// <summary>
        /// Gets a single item by ID
        /// </summary>
        public async Task<T?> GetItemByIdAsync<T>(string collectionName, string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
                throw new ArgumentException("Item ID cannot be null or empty", nameof(itemId));

            try
            {
                // Get existing collection
                var collection = await GetCollectionAsync<T>(collectionName);
                if (collection == null)
                {
                    return default;
                }

                // Find item using IEntityIdentifier when possible
                foreach (var item in collection)
                {
                    // Try for custom identifier interfaces first
                    if (item is IEntityIdentifier<string> stringIdentifiable &&
                        stringIdentifiable.Id == itemId)
                    {
                        return item;
                    }

                    // Fall back to reflection only if necessary
                    var idProperty = item?.GetType().GetProperty("Id");
                    if (idProperty != null)
                    {
                        var existingId = idProperty.GetValue(item)?.ToString();
                        if (existingId == itemId)
                        {
                            return item;
                        }
                    }
                }

                return default;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting item {ItemId} from collection {CollectionName}", itemId, collectionName);
                throw;
            }
        }

        private T? DeserializeJson<T>(string json)
        {
            try
            {
                // Use the injected options
                return JsonSerializer.Deserialize<T>(json, _jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deserializing JSON: {Message}", ex.Message);
                return default;
            }
        }
    }
}
