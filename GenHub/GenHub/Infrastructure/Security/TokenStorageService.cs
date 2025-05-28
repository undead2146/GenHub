using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using GenHub.Core.Interfaces.GitHub;
using GenHub.Core.Interfaces.Caching;
using GenHub.Core.Helpers;

namespace GenHub.Infrastructure.Security
{
    /// <summary>
    /// Service for managing secure token storage
    /// </summary>
    public class TokenStorageService : ITokenStorageService
    {
        private readonly ILogger<TokenStorageService> _logger;
        private readonly string _tokenFilePath;
        private readonly byte[] _entropy = new byte[] { 0x43, 0x71, 0x85, 0x49, 0xAB, 0xCD, 0xEF, 0x12 }; // For encryption
        private readonly ICacheService _cacheService;

        /// <summary>
        /// Event that fires when a token save error occurs
        /// </summary>
        public event EventHandler? TokenSaveError;

        public TokenStorageService(ILogger<TokenStorageService> logger, ICacheService cacheService)
        {
            _logger = logger;
            _cacheService = cacheService;

            // Set up the token storage location in user's app data
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appDataPath, "GenHub");

            if (!Directory.Exists(appFolder))
            {
                Directory.CreateDirectory(appFolder);
            }

            _tokenFilePath = Path.Combine(appFolder, "github.token");
        }

        /// <summary>
        /// Gets the current token
        /// </summary>
        public async Task<string?> GetTokenAsync()
        {
            try
            {
                // Use ConfigureAwait(false) to avoid forcing continuation back to the UI thread
                var encryptedToken = await _cacheService.GetSharedSettingAsync<string>("github_token").ConfigureAwait(false);

                if (string.IsNullOrEmpty(encryptedToken))
                    return null;

                return SimpleEncryption.Decrypt(encryptedToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting GitHub token");
                return null;
            }
        }

        /// <summary>
        /// Saves a token
        /// </summary>
        public async Task SaveTokenAsync(string token)
        {
            try
            {
                var encryptedToken = SimpleEncryption.Encrypt(token);
                // Use ConfigureAwait(false) to avoid forcing continuation back to the UI thread
                await _cacheService.SaveSharedSettingAsync("github_token", encryptedToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving GitHub token");
                TokenSaveError?.Invoke(this, new EventArgs());
            }
        }

        /// <summary>
        /// Clears the stored token
        /// </summary>
        public async Task ClearTokenAsync()
        {
            try
            {
                if (File.Exists(_tokenFilePath))
                {
                    File.Delete(_tokenFilePath);
                    _logger.LogInformation("GitHub token cleared successfully");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing GitHub token");
            }
        }

        /// <summary>
        /// Checks if a token exists
        /// </summary>
        public async Task<bool> HasTokenAsync()
        {
            try
            {
                var token = await _cacheService.GetSharedSettingAsync<string>("github_token");
                return !string.IsNullOrEmpty(token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking token existence");
                return false;
            }
        }
    }
}
