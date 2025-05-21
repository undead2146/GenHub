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
        /// Event raised when a token is missing
        /// </summary>
        event EventHandler? TokenMissing;

        /// <summary>
        /// Event raised when a token is invalid
        /// </summary>
        event EventHandler? TokenInvalid;

        /// <summary>
        /// Gets the current authentication token
        /// </summary>
        string GetCurrentToken();

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
