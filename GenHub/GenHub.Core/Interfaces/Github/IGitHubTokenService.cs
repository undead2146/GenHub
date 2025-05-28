using System;
using System.Threading.Tasks;

namespace GenHub.Core.Interfaces.GitHub
{
    /// <summary>
    /// Service for managing GitHub authentication tokens
    /// </summary>
    public interface IGitHubTokenService
    {
        /// <summary>
        /// Event that fires when a token is missing and user intervention is needed
        /// </summary>
        event EventHandler? TokenMissing;

        /// <summary>
        /// Event that fires when a token is invalid and user intervention is needed
        /// </summary>
        event EventHandler? TokenInvalid;
        /// <summary>
        /// Gets the current authentication token
        /// </summary>
        Task<string> GetCurrentToken();

        /// <summary>
        /// Sets and saves a new authentication token
        /// </summary>
        Task<bool> SetAndSaveTokenAsync(string token);

        /// <summary>
        /// Tests if the current token is valid
        /// </summary>
        Task<bool> TestTokenAsync();

        /// <summary>
        /// Shows a token configuration dialog
        /// </summary>
        Task<(bool Success, string? Token)> ConfigureTokenAsync();
        
    }
}
