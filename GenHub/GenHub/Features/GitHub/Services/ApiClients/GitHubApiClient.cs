using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
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
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
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
        /// Tests the current authentication token by making a simple API call
        /// </summary>
        public async Task<bool> TestAuthenticationAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await EnsureAuthenticationAsync();

                if (string.IsNullOrEmpty(_authToken))
                {
                    _logger.LogWarning("No authentication token available for testing");
                    return false;
                }

                // Make a simple authenticated request to test the token
                var response = await SendRequestAsync(HttpMethod.Get, "user", cancellationToken: cancellationToken);

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _logger.LogWarning("Authentication token test failed - unauthorized");
                    TokenInvalid?.Invoke(this, EventArgs.Empty);
                    return false;
                }

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogDebug("Authentication token test successful");
                    return true;
                }

                _logger.LogWarning("Authentication token test failed with status: {StatusCode}", response.StatusCode);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing authentication token");
                return false;
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

            try
            {
                _authToken = token;
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                // Persist the token using token storage service with ConfigureAwait(false)
                await _tokenStorage.SaveTokenAsync(token).ConfigureAwait(false);
                _logger.LogInformation("GitHub API token set and saved to storage");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting and saving GitHub token");
                throw; // Re-throw to let caller handle
            }
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
        public async Task<T?> GetAsync<T>(GitHubRepository repoSettings, string endpoint, CancellationToken cancellationToken = default) where T : class
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
                using var timeoutSource = new CancellationTokenSource(DefaultTimeout);
                using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken.CanBeCanceled ? cancellationToken : CancellationToken.None,
                    timeoutSource.Token);

                await EnsureAuthenticationAsync();

                string fullUrl = GetFullApiUrl(endpoint);
                _logger.LogDebug("Making GitHub API request to full URL: {Url}", fullUrl);

                var response = await _httpClient.GetAsync(
                    fullUrl,
                    HttpCompletionOption.ResponseHeadersRead,
                    linkedSource.Token);

                await HandleRateLimiting(response);

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    TokenInvalid?.Invoke(this, EventArgs.Empty);
                    return default;
                }

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("GitHub API request failed: {StatusCode} for endpoint: {Endpoint}, Full URL: {FullUrl}",
                        response.StatusCode, endpoint, fullUrl);
                    
                    var errorContent = await response.Content.ReadAsStringAsync(linkedSource.Token);
                    _logger.LogError("Error response content: {ErrorContent}", errorContent);
                    return default;
                }

                if (typeof(T) == typeof(HttpResponseMessage))
                {
                    return response as T;
                }

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
                return default;
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
        public async Task<(Stream Stream, long? ContentLength)> GetStreamAsync(GitHubRepository repoSettings, string uri, CancellationToken cancellationToken = default)
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
        public async Task<IEnumerable<GitHubArtifact>> GetArtifactsForRunAsync(
            GitHubRepository repoConfig,
            long runId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Getting artifacts for run {RunId} from {Owner}/{Repo}",
                    runId, repoConfig.RepoOwner, repoConfig.RepoName);

                var url = $"repos/{repoConfig.RepoOwner}/{repoConfig.RepoName}/actions/runs/{runId}/artifacts";

                var response = await GetAsync<HttpResponseMessage>(repoConfig, url, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get artifacts for run {RunId}: {StatusCode}", runId, response.StatusCode);
                    return Enumerable.Empty<GitHubArtifact>();
                }

                var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);

                if (string.IsNullOrWhiteSpace(jsonContent))
                {
                    _logger.LogWarning("Empty response when getting artifacts for run {RunId}", runId);
                    return Enumerable.Empty<GitHubArtifact>();
                }

                var artifacts = ParseArtifactsFromJson(jsonContent);
                var artifactsList = artifacts.ToList();

                _logger.LogDebug("Successfully retrieved {Count} artifacts for run {RunId}", artifactsList.Count, runId);

                return artifactsList;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting artifacts for run {RunId}", runId);
                return Enumerable.Empty<GitHubArtifact>();
            }
        }

        /// <summary>
        /// Parses artifacts from JSON response with enhanced validation
        /// </summary>
        private IEnumerable<GitHubArtifact> ParseArtifactsFromJson(string jsonContent)
        {
            var artifacts = new List<GitHubArtifact>();

            try
            {
                var document = JsonDocument.Parse(jsonContent);

                if (!document.RootElement.TryGetProperty("artifacts", out var artifactsElement))
                {
                    _logger.LogWarning("No 'artifacts' property found in JSON response");
                    return artifacts;
                }

                var artifactsArray = artifactsElement.EnumerateArray().ToList();
                _logger.LogDebug("Found {Count} artifacts in JSON array", artifactsArray.Count);

                foreach (var (artifactElement, index) in artifactsArray.Select((a, i) => (a, i)))
                {
                    try
                    {
                        var artifact = new GitHubArtifact
                        {
                            Id = artifactElement.TryGetProperty("id", out var idProp) ? idProp.GetInt64() : 0,
                            Name = artifactElement.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? "Unknown Artifact" : "Unknown Artifact",
                            SizeInBytes = artifactElement.TryGetProperty("size_in_bytes", out var sizeProp) ? sizeProp.GetInt64() : 0,
                            WorkflowNumber = 0 // This will be set later by the artifact reader
                        };

                        // Parse created_at
                        if (artifactElement.TryGetProperty("created_at", out var createdAtProp) &&
                            createdAtProp.ValueKind != JsonValueKind.Null)
                        {
                            var rawCreatedAt = createdAtProp.GetString();
                            if (DateTime.TryParse(rawCreatedAt, out var createdAt))
                            {
                                artifact.CreatedAt = createdAt;
                            }
                        }

                        // Parse archive_download_url
                        if (artifactElement.TryGetProperty("archive_download_url", out var urlProp))
                        {
                            artifact.ArchiveDownloadUrl = urlProp.GetString();
                        }

                        // Parse expired
                        if (artifactElement.TryGetProperty("expired", out var expiredProp))
                        {
                            artifact.Expired = expiredProp.GetBoolean();
                        }

                        artifacts.Add(artifact);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error parsing artifact at index {Index}", index);
                    }
                }

                _logger.LogDebug("Successfully parsed {Count} artifacts", artifacts.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing artifacts from JSON");
            }

            return artifacts;
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
                    RepositoryInfo = new GitHubRepository
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
        public async Task<GitHubWorkflow?> GetWorkflowRunAsync(GitHubRepository repoSettings, long runId, CancellationToken cancellationToken = default)
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
        public async Task<HttpResponseMessage> GetRawAsync(GitHubRepository repoSettings, string endpoint, CancellationToken cancellationToken = default)
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
                try
                {
                    var token = await _tokenStorage.GetTokenAsync().ConfigureAwait(false);

                    if (string.IsNullOrEmpty(token))
                    {
                        TokenMissing?.Invoke(this, EventArgs.Empty);
                        return;
                    }

                    // Use a local method to avoid threading issues
                    SetAuthTokenInternal(token);
                }
                catch (Exception ex)
                {
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

        /// <summary>
        /// Gets repository information using the existing API pattern
        /// </summary>
        public async Task<GitHubRepository?> GetRepositoryInfoAsync(string owner, string name, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Getting repository information for {Owner}/{Name}", owner, name);

                var endpoint = $"repos/{owner}/{name}";
                var apiResponse = await GetAsync<GitHubRepositoryApiResponse>(endpoint, cancellationToken);

                if (apiResponse == null)
                {
                    _logger.LogWarning("No repository information found for {Owner}/{Name}", owner, name);
                    return null;
                }

                // Convert API response to our domain model with ALL important properties
                var repository = new GitHubRepository
                {
                    RepoOwner = owner,
                    RepoName = name,
                    DisplayName = apiResponse.FullName ?? $"{owner}/{name}",
                    Branch = apiResponse.DefaultBranch ?? "main",
                    Description = apiResponse.Description,
                    Id = apiResponse.Id,
                    IsPrivate = apiResponse.Private,
                    IsFork = apiResponse.Fork,
                    CloneUrl = apiResponse.CloneUrl,
                    CreatedAt = apiResponse.CreatedAt,
                    UpdatedAt = apiResponse.UpdatedAt,
                    PushedAt = apiResponse.PushedAt,
                    Language = apiResponse.Language,
                    StargazersCount = apiResponse.StargazersCount,
                    ForksCount = apiResponse.ForksCount,
                    WatchersCount = apiResponse.WatchersCount,
                    OpenIssuesCount = apiResponse.OpenIssuesCount,
                    Size = apiResponse.Size,
                    Topics = apiResponse.Topics?.ToArray(),
                    HasIssues = apiResponse.HasIssues,
                    HasProjects = apiResponse.HasProjects,
                    HasWiki = apiResponse.HasWiki,
                    License = apiResponse.License?.Name,
                    DefaultBranch = apiResponse.DefaultBranch,
                    IsArchived = apiResponse.Archived,
                    IsDisabled = apiResponse.Disabled,
                    Enabled = true,
                    LastAccessed = DateTime.UtcNow
                };

                repository.Normalize();

                _logger.LogDebug("Successfully retrieved repository information for {DisplayName} (Size: {Size}KB)", 
                    repository.DisplayName, repository.Size);
                return repository;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting repository information for {Owner}/{Name}", owner, name);
                return null;
            }
        }

        /// <summary>
        /// Gets forks of a repository using existing API patterns
        /// </summary>
        public async Task<IEnumerable<GitHubRepository>?> GetRepositoryForksAsync(string owner, string name, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Getting forks for repository {Owner}/{Name}", owner, name);

                var endpoint = $"repos/{owner}/{name}/forks?sort=newest&per_page=100";
                _logger.LogInformation("Fork API endpoint being called: {Endpoint}", endpoint);
                
                var apiResponse = await GetAsync<IEnumerable<GitHubRepositoryApiResponse>>(endpoint, cancellationToken);

                if (apiResponse == null)
                {
                    _logger.LogDebug("No forks found for {Owner}/{Name}", owner, name);
                    return Enumerable.Empty<GitHubRepository>();
                }

                var forks = apiResponse.Select(fork => new GitHubRepository
                {
                    RepoOwner = fork.Owner?.Login ?? string.Empty,
                    RepoName = fork.Name ?? string.Empty,
                    DisplayName = fork.FullName ?? $"{fork.Owner?.Login}/{fork.Name}",
                    Branch = fork.DefaultBranch ?? "main",
                    Description = fork.Description,
                    Id = fork.Id,
                    IsPrivate = fork.Private,
                    IsFork = true,
                    CloneUrl = fork.CloneUrl,
                    CreatedAt = fork.CreatedAt,
                    UpdatedAt = fork.UpdatedAt,
                    PushedAt = fork.PushedAt,
                    Language = fork.Language,
                    StargazersCount = fork.StargazersCount,
                    ForksCount = fork.ForksCount,
                    WatchersCount = fork.WatchersCount,
                    OpenIssuesCount = fork.OpenIssuesCount,
                    Size = fork.Size,
                    Topics = fork.Topics?.ToArray(),
                    HasIssues = fork.HasIssues,
                    HasProjects = fork.HasProjects,
                    HasWiki = fork.HasWiki,
                    License = fork.License?.Name,
                    DefaultBranch = fork.DefaultBranch,
                    IsArchived = fork.Archived,
                    IsDisabled = fork.Disabled,
                    Enabled = true,
                    LastAccessed = DateTime.UtcNow
                }).Where(f => f.IsValid && !f.IsArchived && !f.IsDisabled).ToList();

                foreach (var fork in forks)
                {
                    fork.Normalize();
                }

                _logger.LogInformation("Successfully found {Count} valid forks for {Owner}/{Name}", forks.Count, owner, name);
                return forks;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting forks for {Owner}/{Name}", owner, name);
                return Enumerable.Empty<GitHubRepository>();
            }
        }

        /// <summary>
        /// Searches for repositories using existing API patterns
        /// </summary>
        public async Task<IEnumerable<GitHubRepository>?> SearchRepositoriesAsync(string query, string sortBy = "best-match", CancellationToken cancellationToken = default)
        {
            try
            {
                var endpoint = $"search/repositories?q={Uri.EscapeDataString(query)}&sort={sortBy}";
                var searchResult = await GetAsync<GitHubSearchResponse>(endpoint, cancellationToken);

                if (searchResult?.Items == null) return Enumerable.Empty<GitHubRepository>();

                return searchResult.Items.Select(item => new GitHubRepository
                {
                    RepoOwner = item.Owner?.Login ?? string.Empty,
                    RepoName = item.Name ?? string.Empty,
                    Description = item.Description,
                    Branch = item.DefaultBranch,
                    Id = item.Id,
                    IsPrivate = item.Private,
                    IsFork = item.Fork,
                    CloneUrl = item.CloneUrl,
                    CreatedAt = item.CreatedAt,
                    UpdatedAt = item.UpdatedAt,
                    PushedAt = item.PushedAt,
                    Language = item.Language,
                    StargazersCount = item.StargazersCount,
                    ForksCount = item.ForksCount,
                    WatchersCount = item.WatchersCount,
                    OpenIssuesCount = item.OpenIssuesCount,
                    Size = item.Size, // CRITICAL: Map the repository size for search results
                    Topics = item.Topics?.ToArray(),
                    HasIssues = item.HasIssues,
                    HasProjects = item.HasProjects,
                    HasWiki = item.HasWiki,
                    License = item.License?.Name,
                    DefaultBranch = item.DefaultBranch,
                    IsArchived = item.Archived,
                    IsDisabled = item.Disabled
                }).Where(r => r.IsValid && !r.IsArchived && !r.IsDisabled);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching for repositories with query: {Query}", query);
                return null;
            }
        }

        /// <summary>
        /// Gets recent workflow runs for a repository
        /// </summary>
        public async Task<IEnumerable<GitHubWorkflow>?> GetWorkflowRunsForRepositoryAsync(string owner, string repo, int perPage = 5, CancellationToken cancellationToken = default)
        {
            try
            {
                // Clean endpoint construction
                var endpoint = $"repos/{owner}/{repo}/actions/runs?per_page={perPage}&status=success";
                var response = await GetAsync<GitHubWorkflowRunsResponse>(endpoint, cancellationToken);
                return response?.WorkflowRuns;
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound || ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                _logger.LogWarning("Workflows not found or disabled for repository {Owner}/{Repo}.", owner, repo);
                return Enumerable.Empty<GitHubWorkflow>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting workflow runs for {Owner}/{Repo}", owner, repo);
                return null;
            }
        }

        /// <summary>
        /// Gets recent releases for a repository - ONLY returns releases with downloadable assets
        /// </summary>
        public async Task<IEnumerable<GitHubRelease>?> GetReleasesForRepositoryAsync(string owner, string repo, int perPage = 5, CancellationToken cancellationToken = default)
        {
            try
            {
                var endpoint = $"repos/{owner}/{repo}/releases?per_page={perPage}";
                var response = await GetAsync<List<GitHubRelease>>(endpoint, cancellationToken);
                
                if (response == null) return Enumerable.Empty<GitHubRelease>();
                
                var validReleases = response.Where(r => 
                    r.Assets?.Any() == true &&
                    r.Assets.Any(a => !string.IsNullOrEmpty(a.BrowserDownloadUrl))
                ).ToList();
                
                _logger.LogDebug("Repository {Owner}/{Repo}: Found {Total} releases, {Valid} with assets", 
                    owner, repo, response.Count, validReleases.Count);
                
                return validReleases;
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogDebug("No releases found for repository {Owner}/{Repo}", owner, repo);
                return Enumerable.Empty<GitHubRelease>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting releases for {Owner}/{Repo}", owner, repo);
                return null;
            }
        }


        /// <summary>
        /// API Response models for GitHub repository data
        /// </summary>
        private class GitHubRepositoryApiResponse
        {
            public long Id { get; set; }
            public string? Name { get; set; }
            public string? FullName { get; set; }
            public string? Description { get; set; }
            public bool Private { get; set; }
            public bool Fork { get; set; }
            public string? DefaultBranch { get; set; }
            public string? HtmlUrl { get; set; }
            public string? CloneUrl { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime UpdatedAt { get; set; }
            public DateTime? PushedAt { get; set; }
            public long Size { get; set; } // Repository size in KB - CRITICAL property
            public int StargazersCount { get; set; }
            public int WatchersCount { get; set; }
            public int ForksCount { get; set; }
            public int OpenIssuesCount { get; set; }
            public bool HasIssues { get; set; }
            public bool HasProjects { get; set; }
            public bool HasWiki { get; set; }
            public bool Archived { get; set; }
            public bool Disabled { get; set; }
            public string? Language { get; set; }
            public GitHubUserApiResponse? Owner { get; set; }
            public GitHubRepositoryApiResponse? Parent { get; set; }
            public GitHubRepositoryApiResponse? Source { get; set; }
            public List<string>? Topics { get; set; }
            public GitHubLicenseInfo? License { get; set; }
        }

        /// <summary>
        /// API Response model for license information
        /// </summary>
        private class GitHubLicenseInfo
        {
            public string? Key { get; set; }
            public string? Name { get; set; }
            public string? SpdxId { get; set; }
            public string? Url { get; set; }
        }

        /// <summary>
        /// API Response model for GitHub user data
        /// </summary>
        private class GitHubUserApiResponse
        {
            public long Id { get; set; }
            public string? Login { get; set; }
            public string? Type { get; set; }
            public string? AvatarUrl { get; set; }
            public string? HtmlUrl { get; set; }
        }

        /// <summary>
        /// API Response model for GitHub search results
        /// </summary>
        private class GitHubSearchResponse
        {
            public int TotalCount { get; set; }
            public bool IncompleteResults { get; set; }
            public List<GitHubRepositoryApiResponse> Items { get; set; } = new();
        }

        private class GitHubWorkflowRunsResponse
        {
            public int TotalCount { get; set; }
            public List<GitHubWorkflow> WorkflowRuns { get; set; } = new();
        }
    }
}
