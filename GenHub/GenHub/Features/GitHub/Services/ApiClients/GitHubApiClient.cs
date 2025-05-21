using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using GenHub.Core.Interfaces.GitHub;
using GenHub.Core.Models;
using GenHub.Core.Models.AppUpdate;
using GenHub.Core.Interfaces;
using GenHub.Core.Models.GitHub;

namespace GenHub.Features.GitHub.Services
{
    /// <summary>
    /// Implementation of the GitHub API client
    /// </summary>
    public class GitHubApiClient : IGitHubApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<GitHubApiClient> _logger;
        private readonly ITokenStorageService _tokenStorage;
        private readonly JsonSerializerOptions _jsonOptions;
        private string? _authToken;
        
        private const string GitHubApiBaseUrl = "https://api.github.com/";
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);
        
        public event EventHandler? TokenMissing;
        public event EventHandler? TokenInvalid;
        
        public bool IsAuthenticated => !string.IsNullOrEmpty(_authToken);
        
        /// <summary>
        /// Creates a new instance of GitHubApiClient
        /// </summary>
        public GitHubApiClient(
            HttpClient httpClient,
            ILogger<GitHubApiClient> logger,
            ITokenStorageService tokenStorage)
        {
            _httpClient = httpClient;
            _logger = logger;
            _tokenStorage = tokenStorage;
            
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            
            // Configure HttpClient
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("GenHub/1.0");
            _httpClient.Timeout = DefaultTimeout; // Set explicit timeout
            
            _logger.LogDebug("GitHubApiClient initialized - token will be loaded on first use");
        }
        /// <summary>
/// Gets the current rate limit information from the GitHub API
/// </summary>
public async Task<RateLimitInfo?> GetRateLimitAsync(CancellationToken cancellationToken = default)
{
    try
    {
        var response = await SendRequestAsync(
            HttpMethod.Get, 
            "rate_limit", 
            cancellationToken: cancellationToken);
            
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Failed to get rate limit info: {StatusCode}", response.StatusCode);
            return null;
        }
        
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var rateLimitInfo = JsonSerializer.Deserialize<RateLimitInfo>(content, _jsonOptions);
        
        return rateLimitInfo;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error getting rate limit information");
        return null;
    }
}

        /// <summary>
        /// Authenticates with the GitHub API using the provided token
        /// </summary>
        public void Authenticate(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                TokenMissing?.Invoke(this, EventArgs.Empty);
                return;
            }
            
            _authToken = token;
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            _logger.LogInformation("GitHub API client authenticated");
        }
        
        /// <summary>
        /// Gets the current authentication token
        /// </summary>
        public string? GetAuthToken()
        {
            return _authToken;
        }
        /// <summary>
        /// Sets the authentication token and persists it to storage
        /// </summary>
        public async Task SetAuthTokenAsync(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
            TokenMissing?.Invoke(this, EventArgs.Empty);
            return;
            }
            
            _authToken = token;
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            
            // Persist the token using token storage service
            await _tokenStorage.SaveTokenAsync(token);
            _logger.LogInformation("GitHub API token set and saved to storage");
        }
        
        /// <summary>
        /// Checks if the GitHub API token exists
        /// </summary>
        public bool HasAuthToken()
        {
            return !string.IsNullOrEmpty(_authToken);
        }
        
        /// <summary>
        /// Generic method to make a GET request to the GitHub API with a repository context and deserialize the response
        /// </summary>
        public async Task<T?> GetAsync<T>(GitHubRepoSettings repoSettings, string endpoint, CancellationToken cancellationToken = default) where T : class
        {
            try
            {
                // Check if the endpoint already starts with "repos/"
                string fullEndpoint;
                if (endpoint.StartsWith("repos/", StringComparison.OrdinalIgnoreCase))
                {
                    fullEndpoint = endpoint;
                }
                else if (endpoint.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    // If it's a full URL, use as-is
                    return await GetAsync<T>(endpoint, cancellationToken);
                }
                else
                {
                    // Otherwise construct the full repository endpoint
                    fullEndpoint = $"repos/{repoSettings.RepoOwner}/{repoSettings.RepoName}/{endpoint}";
                }

                _logger.LogDebug("Making API request to endpoint: {Endpoint}", fullEndpoint);
                
                // Make the request with the constructed endpoint
                return await GetAsync<T>(fullEndpoint, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetAsync<T> with repository context: {RepoOwner}/{RepoName}/{Endpoint}", 
                    repoSettings.RepoOwner, repoSettings.RepoName, endpoint);
                return default;
            }
        }
        
        /// <summary>
        /// Generic method to make a GET request to the GitHub API and deserialize the response
        /// </summary>
        public async Task<T?> GetAsync<T>(string endpoint, CancellationToken cancellationToken = default) where T : class
        {
            try
            {
                // Make sure we have a cancellation token with timeout
                using var timeoutSource = new CancellationTokenSource(DefaultTimeout);
                using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken.CanBeCanceled ? cancellationToken : CancellationToken.None, 
                    timeoutSource.Token);
                
                await EnsureAuthenticationAsync();
                
                string fullUrl = GetFullApiUrl(endpoint);
                _logger.LogDebug("Making GitHub API request to: {Url}", fullUrl);
                
                var response = await _httpClient.GetAsync(
                    fullUrl,
                    HttpCompletionOption.ResponseHeadersRead, // Don't buffer the entire response
                    linkedSource.Token);
                
                await HandleRateLimiting(response);
                
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    TokenInvalid?.Invoke(this, EventArgs.Empty);
                    return default;
                }
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("GitHub API request failed: {StatusCode} for {Endpoint}", 
                        response.StatusCode, endpoint);
                    return default;
                }
                
                // Handle the special case where T is HttpResponseMessage
                if (typeof(T) == typeof(HttpResponseMessage))
                {
                    return response as T;
                }
                
                // Check content length to avoid empty response errors
                var contentLength = response.Content.Headers.ContentLength;
                if (contentLength.HasValue && contentLength.Value <= 0)
                {
                    _logger.LogWarning("Empty content received from GitHub API: {Endpoint}", endpoint);
                    return default;
                }
                
                using var stream = await response.Content.ReadAsStreamAsync(linkedSource.Token);
                return await JsonSerializer.DeserializeAsync<T>(stream, _jsonOptions, linkedSource.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Request for {Endpoint} timed out or was cancelled", endpoint);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling GitHub API for {Endpoint}", endpoint);
                return default;
            }
        }
        
        /// <summary>
        /// Generic method to make a GET request to the GitHub API using owner and repo directly
        /// </summary>
        public async Task<T?> GetAsync<T>(string owner, string repo, string endpoint, CancellationToken cancellationToken = default) where T : class
        {
            string repoEndpoint = $"repos/{owner}/{repo}/{endpoint}";
            return await GetAsync<T>(repoEndpoint, cancellationToken);
        }
        
        /// <summary>
        /// Gets a stream for a remote file
        /// </summary>
        public async Task<(Stream Stream, long? ContentLength)> GetStreamAsync(GitHubRepoSettings repoSettings, string uri, CancellationToken cancellationToken = default)
        {
            try
            {
                await EnsureAuthenticationAsync();
                
                // If URI is already a complete URL, use it as is
                Uri requestUri;
                if (Uri.TryCreate(uri, UriKind.Absolute, out var absoluteUri))
                {
                    requestUri = absoluteUri;
                }
                else
                {
                    // Otherwise, treat it as a relative path within the repo
                    requestUri = new Uri(GetFullApiUrl($"repos/{repoSettings.RepoOwner}/{repoSettings.RepoName}/{uri}"));
                }
                
                // Make sure to use ResponseHeadersRead to avoid loading entire response into memory
                var response = await _httpClient.GetAsync(
                    requestUri,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);
                
                await HandleRateLimiting(response);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("GitHub API stream request failed: {StatusCode} for {Uri}", 
                        response.StatusCode, uri);
                    return (Stream.Null, null);
                }
                
                var contentLength = response.Content.Headers.ContentLength;
                var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                
                return (stream, contentLength);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Stream request timed out or was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting stream from GitHub API for {Uri}", uri);
                return (Stream.Null, null);
            }
        }
        
        /// <summary>
        /// Gets artifacts for a specific run
        /// </summary>
        public async Task<IEnumerable<GitHubArtifact>> GetArtifactsForRunAsync(GitHubRepoSettings repoSettings, long runId, CancellationToken cancellationToken = default)
        {
            return await GetArtifactsForWorkflowRunAsync(repoSettings.RepoOwner, repoSettings.RepoName, runId, cancellationToken);
        }
        
        /// <summary>
        /// Handles API rate limiting
        /// </summary>
        public async Task<bool> HandleRateLimiting(HttpResponseMessage response)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                // Check if rate limited
                if (response.Headers.TryGetValues("X-RateLimit-Remaining", out var remainingValues) &&
                    int.TryParse(remainingValues.FirstOrDefault(), out int remaining) &&
                    remaining == 0)
                {
                    // We are rate limited
                    if (response.Headers.TryGetValues("X-RateLimit-Reset", out var resetValues) &&
                        long.TryParse(resetValues.FirstOrDefault(), out long resetEpoch))
                    {
                        var resetTime = DateTimeOffset.FromUnixTimeSeconds(resetEpoch).LocalDateTime;
                        var waitTime = resetTime - DateTime.Now;
                        
                        if (waitTime.TotalSeconds > 0 && waitTime.TotalMinutes < 5) // Only wait for reasonable times
                        {
                            _logger.LogWarning("GitHub API rate limit exceeded. Waiting for {WaitTime} until {ResetTime}",
                                waitTime, resetTime);
                                
                            // Instead of blocking, just return false to indicate rate limiting
                            return false;
                        }
                        else
                        {
                            _logger.LogError("GitHub API rate limit exceeded. Reset time is {ResetTime} which is too far in the future.",
                                resetTime);
                        }
                    }
                }
            }
            
            return true; // No rate limiting or handled rate limiting
        }
        
        /// <summary>
        /// Gets artifacts for a workflow run
        /// </summary>
        public async Task<IEnumerable<GitHubArtifact>> GetArtifactsForWorkflowRunAsync(string owner, string repo, long runId, CancellationToken cancellationToken = default)
        {
            try
            {
                var endpoint = $"repos/{owner}/{repo}/actions/runs/{runId}/artifacts";
                var response = await GetAsync<GitHubArtifactResponse>(endpoint, cancellationToken);
                
                if (response?.Artifacts == null)
                    return new List<GitHubArtifact>();
                
                // Convert from API response to our domain model
                return response.Artifacts.Select(a => new GitHubArtifact
                {
                    Id = a.Id,
                    Name = a.Name,
                    SizeInBytes = a.SizeInBytes,
                    ArchiveDownloadUrl = a.ArchiveDownloadUrl,
                    RunId = runId,
                    RepositoryInfo = new GitHubRepoSettings
                    {
                        RepoOwner = owner,
                        RepoName = repo,
                        DisplayName = $"{owner}/{repo}"
                    }
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting artifacts for workflow run {RunId} in {Owner}/{Repo}", 
                    runId, owner, repo);
                return new List<GitHubArtifact>();
            }
        }
        
        /// <summary>
        /// Gets a specific workflow run by its ID
        /// </summary>
        public async Task<GitHubWorkflow?> GetWorkflowRunAsync(GitHubRepoSettings repoSettings, long runId, CancellationToken cancellationToken = default)
        {
            return await GetWorkflowRunAsync(repoSettings.RepoOwner, repoSettings.RepoName, runId, cancellationToken);
        }
        
        /// <summary>
        /// Gets a specific workflow run by its ID with owner and repo names
        /// </summary>
        public async Task<GitHubWorkflow?> GetWorkflowRunAsync(string owner, string repo, long runId, CancellationToken cancellationToken = default)
        {
            var endpoint = $"actions/runs/{runId}";
            return await GetAsync<GitHubWorkflow>(owner, repo, endpoint, cancellationToken);
        }

  /// <summary>
        /// Gets a raw HTTP response for a GitHub API request
        /// </summary>
        public async Task<HttpResponseMessage> GetRawAsync(GitHubRepoSettings repoSettings, string endpoint, CancellationToken cancellationToken = default)
        {
            try
            {
                await EnsureAuthenticationAsync();
                
                string repoEndpoint = endpoint;
                if (!endpoint.StartsWith("http", StringComparison.OrdinalIgnoreCase) && 
                    !endpoint.StartsWith("repos/", StringComparison.OrdinalIgnoreCase) && 
                    repoSettings != null)
                {
                    repoEndpoint = $"repos/{repoSettings.RepoOwner}/{repoSettings.RepoName}/{endpoint}";
                }
                
                return await _httpClient.GetAsync(
                    GetFullApiUrl(repoEndpoint),
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error making raw GitHub API request: {Endpoint}", endpoint);
                throw;
            }
        }
                /// <summary>
        /// Gets the latest release for a repository
        /// </summary>
        public async Task<GitHubRelease?> GetLatestReleaseAsync(string owner, string repo, CancellationToken cancellationToken = default)
        {
            return await GetAsync<GitHubRelease>($"repos/{owner}/{repo}/releases/latest", cancellationToken);
        }
                /// <summary>
                /// Gets all releases for a repository
                /// </summary>
                public async Task<IEnumerable<GitHubRelease>> GetReleasesAsync(string owner, string repo, CancellationToken cancellationToken = default)
                {
                    var releases = await GetAsync<IEnumerable<GitHubRelease>>($"repos/{owner}/{repo}/releases", cancellationToken);
                    return releases ?? new List<GitHubRelease>();
                }
                
        
        
        /// <summary>
        /// Ensures the client has authentication headers set
        /// </summary>
        private async Task EnsureAuthenticationAsync()
        {
            if (string.IsNullOrEmpty(_authToken))
            {
                try {
                    var token = await _tokenStorage.GetTokenAsync().ConfigureAwait(false);
                    
                    if (string.IsNullOrEmpty(token))
                    {
                        TokenMissing?.Invoke(this, EventArgs.Empty);
                        return;
                    }
                    
                    // Use a local method to avoid threading issues
                    SetAuthTokenInternal(token);
                }
                catch (Exception ex) {
                    _logger.LogError(ex, "Error loading GitHub token");
                    // Don't rethrow - we'll just continue without a token
                }
            }
        }
        
        // Helper method to avoid thread issues
        private void SetAuthTokenInternal(string token)
        {
            if (string.IsNullOrEmpty(token)) return;
            
            _authToken = token;
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        
        /// <summary>
        /// Gets the full API URL for a given endpoint
        /// </summary>
        private string GetFullApiUrl(string endpoint)
        {
            return endpoint.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? endpoint
                : $"{GitHubApiBaseUrl.TrimEnd('/')}/{endpoint.TrimStart('/')}";
        }
        
        /// <summary>
/// Sends a request to the GitHub API with proper authentication and error handling
/// </summary>
private async Task<HttpResponseMessage> SendRequestAsync(HttpMethod method, string endpoint, HttpContent? content = null, CancellationToken cancellationToken = default)
{
    try
    {
        // Ensure we have authentication set up
        await EnsureAuthenticationAsync();
        
        // Create the request message
        var requestUri = GetFullApiUrl(endpoint);
        var request = new HttpRequestMessage(method, requestUri);
        
        // Add content if provided
        if (content != null)
        {
            request.Content = content;
        }
        
        // Send the request
        var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        
        // Log the result
        _logger.LogDebug("GitHub API {Method} request to {Endpoint}: {StatusCode}", 
            method, endpoint, response.StatusCode);
            
        return response;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error sending {Method} request to {Endpoint}", method, endpoint);
        throw;
    }
}
        
        // API Response types
        private class GitHubArtifactResponse
        {
            public int TotalCount { get; set; }
            public List<GitHubArtifactItem> Artifacts { get; set; } = new();
        }
        
        private class GitHubArtifactItem
        {
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public long SizeInBytes { get; set; }
            public string ArchiveDownloadUrl { get; set; } = string.Empty;
        }
    }
}
