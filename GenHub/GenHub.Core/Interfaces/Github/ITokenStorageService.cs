using System;
using System.Threading;
using System.Threading.Tasks;

namespace GenHub.Core.Interfaces.GitHub
{
    /// <summary>
    /// Service for securely storing and retrieving GitHub authentication tokens
    /// </summary>
    public interface ITokenStorageService
    {
        
        /// <summary>
        /// Gets the current GitHub token
        /// </summary>
        Task<string?> GetTokenAsync();
        
        /// <summary>
        /// Saves a GitHub token
        /// </summary>
        Task SaveTokenAsync(string token);
        
        /// <summary>
        /// Clears the stored GitHub token
        /// </summary>
        Task ClearTokenAsync();
        
        /// <summary>
        /// Checks if a token exists
        /// </summary>
        Task<bool> HasTokenAsync();
    }
}
