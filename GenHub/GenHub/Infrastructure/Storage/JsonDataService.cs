using System;
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
    /// JSON-based data service implementation for collection persistence
    /// </summary>
    public class JsonDataService : IJsonDataService
    {
        private readonly ILogger<JsonDataService> _logger;
        private readonly string _dataDirectory;
        private readonly JsonSerializerOptions _jsonOptions;

        public JsonDataService(ILogger<JsonDataService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            _dataDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "GenHub", "Data");
            
            Directory.CreateDirectory(_dataDirectory);
            
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };
            
            _logger.LogDebug("JsonDataService initialized with data directory: {DataDirectory}", _dataDirectory);
        }

        /// <summary>
        /// Loads a collection from a JSON file
        /// </summary>
        public async Task<IEnumerable<T>> LoadCollectionAsync<T>(string fileName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException("File name cannot be null or empty", nameof(fileName));
            }

            try
            {
                string filePath = GetFilePath(fileName);
                
                if (!File.Exists(filePath))
                {
                    _logger.LogDebug("Collection file {FileName} not found, returning empty collection", fileName);
                    return Enumerable.Empty<T>();
                }

                _logger.LogDebug("Loading collection from {FilePath}", filePath);
                
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var collection = await JsonSerializer.DeserializeAsync<List<T>>(fileStream, _jsonOptions, cancellationToken);
                
                var count = collection?.Count ?? 0;
                _logger.LogInformation("Successfully loaded {Count} items from {FileName}", count, fileName);
                
                return collection ?? Enumerable.Empty<T>();
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "JSON deserialization error loading collection from {FileName}", fileName);
                throw new InvalidOperationException($"Failed to deserialize JSON from {fileName}: {jsonEx.Message}", jsonEx);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error loading collection from {FileName}", fileName);
                throw new InvalidOperationException($"Failed to load collection from {fileName}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Saves a collection to a JSON file
        /// </summary>
        public async Task SaveCollectionAsync<T>(string fileName, IEnumerable<T> collection, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException("File name cannot be null or empty", nameof(fileName));
            }

            if (collection == null)
            {
                throw new ArgumentNullException(nameof(collection));
            }

            try
            {
                string filePath = GetFilePath(fileName);
                var collectionList = collection.ToList();
                
                _logger.LogDebug("Saving {Count} items to {FilePath}", collectionList.Count, filePath);
                
                // Ensure directory exists
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Write to temporary file first, then move to prevent corruption
                string tempFilePath = filePath + ".tmp";
                
                using (var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await JsonSerializer.SerializeAsync(fileStream, collectionList, _jsonOptions, cancellationToken);
                    await fileStream.FlushAsync(cancellationToken);
                }
                
                // Atomic move operation
                if (File.Exists(filePath))
                {
                    File.Replace(tempFilePath, filePath, null);
                }
                else
                {
                    File.Move(tempFilePath, filePath);
                }
                
                _logger.LogInformation("Successfully saved {Count} items to {FileName}", collectionList.Count, fileName);
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "JSON serialization error saving collection to {FileName}", fileName);
                throw new InvalidOperationException($"Failed to serialize collection to {fileName}: {jsonEx.Message}", jsonEx);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error saving collection to {FileName}", fileName);
                throw new InvalidOperationException($"Failed to save collection to {fileName}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Loads a single item from a JSON file
        /// </summary>
        public async Task<T?> LoadItemAsync<T>(string fileName, CancellationToken cancellationToken = default) where T : class
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException("File name cannot be null or empty", nameof(fileName));
            }

            try
            {
                string filePath = GetFilePath(fileName);
                
                if (!File.Exists(filePath))
                {
                    _logger.LogDebug("Item file {FileName} not found", fileName);
                    return null;
                }

                _logger.LogDebug("Loading item from {FilePath}", filePath);
                
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var item = await JsonSerializer.DeserializeAsync<T>(fileStream, _jsonOptions, cancellationToken);
                
                _logger.LogDebug("Successfully loaded item from {FileName}", fileName);
                return item;
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "JSON deserialization error loading item from {FileName}", fileName);
                throw new InvalidOperationException($"Failed to deserialize JSON from {fileName}: {jsonEx.Message}", jsonEx);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error loading item from {FileName}", fileName);
                throw new InvalidOperationException($"Failed to load item from {fileName}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Saves a single item to a JSON file
        /// </summary>
        public async Task SaveItemAsync<T>(string fileName, T item, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException("File name cannot be null or empty", nameof(fileName));
            }

            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            try
            {
                string filePath = GetFilePath(fileName);
                
                _logger.LogDebug("Saving item to {FilePath}", filePath);
                
                // Ensure directory exists
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Write to temporary file first, then move to prevent corruption
                string tempFilePath = filePath + ".tmp";
                
                using (var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await JsonSerializer.SerializeAsync(fileStream, item, _jsonOptions, cancellationToken);
                    await fileStream.FlushAsync(cancellationToken);
                }
                
                // Atomic move operation
                if (File.Exists(filePath))
                {
                    File.Replace(tempFilePath, filePath, null);
                }
                else
                {
                    File.Move(tempFilePath, filePath);
                }
                
                _logger.LogDebug("Successfully saved item to {FileName}", fileName);
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "JSON serialization error saving item to {FileName}", fileName);
                throw new InvalidOperationException($"Failed to serialize item to {fileName}: {jsonEx.Message}", jsonEx);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error saving item to {FileName}", fileName);
                throw new InvalidOperationException($"Failed to save item to {fileName}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Checks if a file exists
        /// </summary>
        public bool FileExists(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return false;
            }

            string filePath = GetFilePath(fileName);
            return File.Exists(filePath);
        }

        /// <summary>
        /// Deletes a file
        /// </summary>
        public async Task DeleteFileAsync(string fileName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("File name cannot be null or empty", nameof(fileName));

            try
            {
                string filePath = GetFilePath(fileName);
                
                if (File.Exists(filePath))
                {
                    await Task.Run(() => File.Delete(filePath), cancellationToken);
                    _logger.LogDebug("Deleted file {FileName}", fileName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting file {FileName}", fileName);
                throw new InvalidOperationException($"Failed to delete file {fileName}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Gets the full file path for a given filename
        /// </summary>
        private string GetFilePath(string fileName)
        {
            if (!fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                fileName += ".json";

            return Path.Combine(_dataDirectory, fileName);
        }

        /// <summary>
        /// Loads a collection of items from a JSON file
        /// </summary>
        public async Task<IEnumerable<T>> LoadCategoryAsync<T>(string category, CancellationToken cancellationToken = default)
        {
            return await LoadCollectionAsync<T>($"{category}.json", cancellationToken);
        }

        /// <summary>
        /// Loads an item by key from a specific category
        /// </summary>
        public async Task<T?> LoadAsync<T>(string category, string key, CancellationToken cancellationToken = default) where T : class
        {
            try
            {
                string filePath = Path.Combine(_dataDirectory, category, $"{key}.json");
                
                if (!File.Exists(filePath))
                    return null;

                var json = await File.ReadAllTextAsync(filePath, cancellationToken);
                return JsonSerializer.Deserialize<T>(json, _jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading item {Key} from category {Category}", key, category);
                return null;
            }
        }

        /// <summary>
        /// Saves an item to a specific category
        /// </summary>
        public async Task SaveAsync<T>(string category, string key, T data, CancellationToken cancellationToken = default)
        {
            try
            {
                string categoryPath = Path.Combine(_dataDirectory, category);
                Directory.CreateDirectory(categoryPath);
                
                string filePath = Path.Combine(categoryPath, $"{key}.json");
                string tempFilePath = filePath + ".tmp";
                
                var json = JsonSerializer.Serialize(data, _jsonOptions);
                await File.WriteAllTextAsync(tempFilePath, json, cancellationToken);
                
                // Atomic operation
                if (File.Exists(filePath))
                    File.Replace(tempFilePath, filePath, null);
                else
                    File.Move(tempFilePath, filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving item {Key} to category {Category}", key, category);
                throw;
            }
        }

        /// <summary>
        /// Saves a collection of items to a JSON file in a specific category
        /// </summary>
        public async Task SaveCategoryAsync<T>(string category, IEnumerable<T> data, CancellationToken cancellationToken = default)
        {
            await SaveCollectionAsync($"{category}.json", data, cancellationToken);
        }

        /// <summary>
        /// Deletes an item by key from a specific category
        /// </summary>
        public async Task DeleteAsync(string category, string key, CancellationToken cancellationToken = default)
        {
            try
            {
                string filePath = Path.Combine(_dataDirectory, category, $"{key}.json");
                
                if (File.Exists(filePath))
                {
                    await Task.Run(() => File.Delete(filePath), cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting item {Key} from category {Category}", key, category);
                throw;
            }
        }

        /// <summary>
        /// Checks if an item exists by key in a specific category
        /// </summary>
        public async Task<bool> ExistsAsync(string category, string key, CancellationToken cancellationToken = default)
        {
            try
            {
                string filePath = Path.Combine(_dataDirectory, category, $"{key}.json");
                return await Task.FromResult(File.Exists(filePath));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking existence of item {Key} in category {Category}", key, category);
                return false;
            }
        }
    }
}
