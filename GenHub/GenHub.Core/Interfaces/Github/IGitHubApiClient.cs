using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Models;
using GenHub.Core.Models.GitHub;
using GenHub.Core.Models.AppUpdate;

namespace GenHub.Core.Interfaces.GitHub
{
    /// <summary>
    /// Client for the GitHub API
    /// </summary>
    public interface IGitHubApiClient
    {
        /// <summary>
        /// Event that fires when a token is missing
        /// </summary>
        event EventHandler TokenMissing;
        
        /// <summary>
        /// Event that fires when a token is invalid
        /// </summary>
        event EventHandler TokenInvalid;
        
        /// <summary>
        /// Gets artifacts for a workflow run
        /// </summary>
        Task<IEnumerable<GitHubArtifact>> GetArtifactsForRunAsync(GitHubRepoSettings repoSettings, long runId, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Handles API rate limiting
        /// </summary>
        Task<bool> HandleRateLimiting(HttpResponseMessage response);
        
        /// <summary>
        /// Gets artifacts for a workflow run
        /// </summary>
        Task<IEnumerable<GitHubArtifact>> GetArtifactsForWorkflowRunAsync(string owner, string repo, long runId, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets a stream for downloading content
        /// </summary>
        Task<(Stream Stream, long? ContentLength)> GetStreamAsync(GitHubRepoSettings repoSettings, string endpoint, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Generic method to make a GET request to the GitHub API and deserialize the response
        /// </summary>
        Task<T?> GetAsync<T>(string endpoint, CancellationToken cancellationToken = default) where T : class;
        
        /// <summary>
        /// Generic method to make a GET request to the GitHub API with a repository context and deserialize the response
        /// </summary>
        Task<T?> GetAsync<T>(GitHubRepoSettings repoSettings, string endpoint, CancellationToken cancellationToken = default) where T : class;
        
        /// <summary>
        /// Generic method to make a GET request to the GitHub API using owner and repo directly
        /// </summary>
        Task<T?> GetAsync<T>(string owner, string repo, string endpoint, CancellationToken cancellationToken = default) where T : class;
        
        /// <summary>
        /// Gets a specific workflow run by its ID
        /// </summary>
        Task<GitHubWorkflow?> GetWorkflowRunAsync(GitHubRepoSettings repoSettings, long runId, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets a specific workflow run by its ID with owner and repo names
        /// </summary>
        Task<GitHubWorkflow?> GetWorkflowRunAsync(string owner, string repo, long runId, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets whether the client is authenticated
        /// </summary>
        bool IsAuthenticated { get; }
        
        /// <summary>
        /// Sets the authentication token
        /// </summary>
        Task SetAuthTokenAsync(string token);
        
        /// <summary>
        /// Gets the current authentication token
        /// </summary>
        string? GetAuthToken();
        
        /// <summary>
        /// Gets a raw HTTP response for a GitHub API request
        /// </summary>
        Task<HttpResponseMessage> GetRawAsync(GitHubRepoSettings repoSettings, string endpoint, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets the latest release for a repository
        /// </summary>
        Task<GitHubRelease?> GetLatestReleaseAsync(string owner, string repo, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets all releases for a repository
        /// </summary>
        Task<IEnumerable<GitHubRelease>> GetReleasesAsync(string owner, string repo, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets the current rate limit information from the GitHub API
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>Rate limit information or null if not available</returns>
        Task<RateLimitInfo?> GetRateLimitAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Tests the current authentication token
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>True if authentication is valid, false otherwise</returns>
        Task<bool> TestAuthenticationAsync(CancellationToken cancellationToken = default);
    }
}
