using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using GenHub.Core.Interfaces.GitHub;
using GenHub.Core.Models;
using GenHub.Features.GitHub.Views;
using GenHub.Features.GitHub.ViewModels;
using Avalonia;
using Avalonia.Controls;

namespace GenHub.Features.GitHub.Services
{
    /// <summary>
    /// Service for managing GitHub authentication tokens with UI integration
    /// </summary>
    public class GitHubTokenService : IGitHubTokenService
    {
        private readonly ITokenStorageService _tokenStorage;
        private readonly IGitHubApiClient _apiClient;
        private readonly ILogger<GitHubTokenService> _logger;

        public event EventHandler? TokenMissing;
        public event EventHandler? TokenInvalid;

        public GitHubTokenService(
            ITokenStorageService tokenStorage,
            IGitHubApiClient apiClient,
            ILogger<GitHubTokenService> logger)
        {
            _tokenStorage = tokenStorage ?? throw new ArgumentNullException(nameof(tokenStorage));
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<string> GetCurrentToken()
        {
            try
            {
                var token = await _tokenStorage.GetTokenAsync();
                return token ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current token");
                return string.Empty;
            }
        }

        public async Task<bool> SetAndSaveTokenAsync(string token)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(token))
                {
                    _logger.LogWarning("Attempted to save empty or null token");
                    return false;
                }

                // Test the token before saving
                var oldToken = await _tokenStorage.GetTokenAsync();
                await _tokenStorage.SaveTokenAsync(token);

                // Update the API client with the new token
                await _apiClient.SetAuthTokenAsync(token);

                var isValid = await TestTokenAsync();
                if (!isValid)
                {
                    // Restore old token if the new one is invalid
                    if (!string.IsNullOrEmpty(oldToken))
                    {
                        await _tokenStorage.SaveTokenAsync(oldToken);
                        await _apiClient.SetAuthTokenAsync(oldToken);
                    }
                    else
                    {
                        await _tokenStorage.ClearTokenAsync();
                        await _apiClient.SetAuthTokenAsync(string.Empty);
                    }
                    OnTokenInvalid();
                    return false;
                }

                _logger.LogInformation("GitHub token saved and validated successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving and validating token");
                OnTokenInvalid();
                return false;
            }
        }

        public async Task<bool> TestTokenAsync()
        {
            try
            {
                var token = await _tokenStorage.GetTokenAsync();
                if (string.IsNullOrEmpty(token))
                {
                    _logger.LogWarning("No token available to test");
                    OnTokenMissing();
                    return false;
                }

                // Test the token by making a simple API call
                var testResult = await _apiClient.TestAuthenticationAsync();
                if (!testResult)
                {
                    _logger.LogWarning("Token authentication test failed");
                    OnTokenInvalid();
                }

                return testResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing token");
                OnTokenInvalid();
                return false;
            }
        }

        public async Task<(bool Success, string? Token)> ConfigureTokenAsync()
        {
            try
            {
                _logger.LogInformation("Opening GitHub token configuration dialog");

                // Create the dialog using the constructor that takes dependencies
                var dialog = new GitHubTokenDialogWindow(_apiClient, _logger);

                // Set current token if available
                var currentToken = await _tokenStorage.GetTokenAsync();
                if (!string.IsNullOrEmpty(currentToken))
                {
                    _logger.LogDebug("Setting current token in dialog");
                    dialog.SetToken(currentToken);
                }

                _logger.LogDebug("Showing dialog to user");
                var result = await dialog.ShowDialogAsync();

                if (result?.Success == true && !string.IsNullOrWhiteSpace(result.Token))
                {
                    _logger.LogInformation("User provided token, attempting to save and validate");

                    var saveSuccess = await SetAndSaveTokenAsync(result.Token);
                    if (saveSuccess)
                    {
                        _logger.LogInformation("Token configuration completed successfully");
                        return (true, result.Token);
                    }
                    else
                    {
                        _logger.LogWarning("Token validation failed during configuration");
                        return (false, null);
                    }
                }

                _logger.LogInformation("Token configuration cancelled by user");
                return (false, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during token configuration");
                return (false, null);
            }
        }
        
        /// <summary>
        /// Raises the TokenMissing event
        /// </summary>
        protected virtual void OnTokenMissing()
        {
            _logger.LogDebug("Firing TokenMissing event");
            TokenMissing?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Raises the TokenInvalid event
        /// </summary>
        protected virtual void OnTokenInvalid()
        {
            _logger.LogDebug("Firing TokenInvalid event");
            TokenInvalid?.Invoke(this, EventArgs.Empty);
        }
    }
}
