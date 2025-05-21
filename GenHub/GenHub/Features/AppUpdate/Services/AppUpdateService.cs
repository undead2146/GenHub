using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using GenHub.Core.Interfaces.AppUpdate;
using GenHub.Core.Interfaces.GitHub;
using GenHub.Core.Models;
using GenHub.Core.Models.AppUpdate;
using GenHub.Core.Models.GitHub;
using GenHub.Core.Interfaces.Caching;

namespace GenHub.Features.AppUpdate.Services
{
    /// <summary>
    /// Service for handling application updates
    /// </summary>
    public class AppUpdateService : IAppUpdateService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<AppUpdateService> _logger;
        private readonly IAppVersionService _appVersionService;
        private readonly IVersionComparator _versionComparator;
        private readonly ICacheService _cacheService;
        private readonly IUpdateInstaller _updateInstaller;
        private readonly IGitHubReleaseReader _releaseReader;

        // Cache key for GitHub releases
        private const string RELEASES_CACHE_KEY = "github_releases";
        // Setting key for update repository
        private const string UPDATE_REPO_SETTING = "update_repository";
        // Setting key for repositories collection
        private const string REPOSITORIES_SETTING = "update_repositories";

        // Use the proper constants that align with what GitHubRepositoryManager uses
        private const string REPOSITORIES_KEY = "github_repositories";
        private const string CURRENT_REPOSITORY_KEY = "current_github_repository";

        // Add a repository manager field
        private readonly IGitHubRepositoryManager _repositoryManager;

        /// <summary>
        /// Creates a new instance of AppUpdateService
        /// </summary>
        public AppUpdateService(
            HttpClient httpClient,
            IVersionComparator versionComparator,
            IUpdateInstaller updateInstaller,
            IAppVersionService appVersionService,
            ICacheService cacheService,
            ILogger<AppUpdateService> logger,
            IGitHubRepositoryManager repositoryManager,
            IGitHubReleaseReader? releaseReader = null)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _versionComparator = versionComparator ?? throw new ArgumentNullException(nameof(versionComparator));
            _updateInstaller = updateInstaller ?? throw new ArgumentNullException(nameof(updateInstaller));
            _appVersionService = appVersionService ?? throw new ArgumentNullException(nameof(appVersionService));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _repositoryManager = repositoryManager ?? throw new ArgumentNullException(nameof(repositoryManager));
            _releaseReader = releaseReader; // Optional dependency

            _logger.LogInformation("AppUpdateService initialized");
        }

        /// <summary>
        /// Gets the current application version
        /// </summary>
        public string GetCurrentVersion()
        {
            return _appVersionService.GetCurrentVersion();
        }

        /// <summary>
        /// Check for updates using the default repository
        /// </summary>
        public async Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var settings = await GetRepositorySettingsAsync();
                if (settings == null)
                {
                    _logger.LogWarning("No repository settings found for update check");
                    return new UpdateCheckResult
                    {
                        IsUpdateAvailable = false,
                        ErrorMessages = { "No repository settings configured" }
                    };
                }

                return await CheckForUpdatesAsync(settings.RepoOwner, settings.RepoName, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking for updates");
                return new UpdateCheckResult
                {
                    IsUpdateAvailable = false,
                    ErrorMessages = { $"Error: {ex.Message}" },
                    // Always provide a non-null LatestRelease to prevent binding errors
                    LatestRelease = new GitHubRelease
                    {
                        Name = "Error checking for updates",
                        Version = GetCurrentVersion(),
                        Body = ex.Message
                    }
                };
            }
        }

        /// <summary>
        /// Checks for updates with standard caching behavior
        /// </summary>
        public async Task<UpdateCheckResult> CheckForUpdatesAsync(string owner, string repo, CancellationToken cancellationToken = default)
        {
            try
            {
                // Create a separate CTS to avoid propagating cancellation too aggressively
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

                _logger.LogInformation("Checking for updates from {Owner}/{Repo}", owner, repo);

                // Create a cache key based on the repository
                string cacheKey = $"{RELEASES_CACHE_KEY}_{owner}_{repo}";

                // Try to get the release from the cache first - keep operation separated
                UpdateCheckResult? result;
                try
                {
                    result = await _cacheService.GetOrFetchAsync<UpdateCheckResult>(
                        cacheKey,
                        async () => await FetchUpdateCheckResultAsync(owner, repo, cts.Token),
                        TimeSpan.FromMinutes(5),
                        cts.Token
                    );
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("Update check timed out, trying without cache");
                    // Fallback to direct fetch without cache
                    result = await FetchUpdateCheckResultAsync(owner, repo, CancellationToken.None);
                }

                if (result == null)
                {
                    _logger.LogWarning("Failed to check for updates from {Owner}/{Repo}", owner, repo);
                    return new UpdateCheckResult
                    {
                        IsUpdateAvailable = false,
                        ErrorMessages = { "Failed to retrieve release information" },
                        // Always provide a non-null LatestRelease to prevent binding errors
                        LatestRelease = new GitHubRelease
                        {
                            Name = "Failed to retrieve update information",
                            Version = GetCurrentVersion()
                        }
                    };
                }

                // Ensure LatestRelease is always non-null to prevent binding errors
                if (result.LatestRelease == null)
                {
                    result.LatestRelease = new GitHubRelease
                    {
                        Name = "No release information available",
                        Version = GetCurrentVersion()
                    };
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking for updates from {Owner}/{Repo}", owner, repo);
                return new UpdateCheckResult
                {
                    IsUpdateAvailable = false,
                    ErrorMessages = { $"Error: {ex.Message}" },
                    // Always provide a non-null LatestRelease
                    LatestRelease = new GitHubRelease
                    {
                        Name = "Error checking for updates",
                        Version = GetCurrentVersion(),
                        Body = ex.Message
                    }
                };
            }
        }

        /// <summary>
        /// Checks for updates bypassing cache (useful for retry after timeout)
        /// </summary>
        public async Task<UpdateCheckResult> CheckForUpdatesNoCache(string owner, string repo, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Checking for updates without cache from {Owner}/{Repo}", owner, repo);

                // Directly call fetch method without using cache
                var result = await FetchUpdateCheckResultAsync(owner, repo, cancellationToken);

                if (result == null)
                {
                    _logger.LogWarning("Failed to check for updates (no cache) from {Owner}/{Repo}", owner, repo);
                    return new UpdateCheckResult
                    {
                        IsUpdateAvailable = false,
                        ErrorMessages = { "Failed to retrieve release information" }
                    };
                }

                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Update check operation (no cache) was canceled or timed out for {Owner}/{Repo}", owner, repo);

                // Create and return a default result instead of throwing
                return new UpdateCheckResult
                {
                    IsUpdateAvailable = false,
                    ErrorMessages = { "Operation was canceled or timed out" }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking for updates (no cache) from {Owner}/{Repo}", owner, repo);
                return new UpdateCheckResult
                {
                    IsUpdateAvailable = false,
                    ErrorMessages = { $"Error: {ex.Message}" }
                };
            }
        }

        /// <summary>
        /// Fetches update check result directly from source
        /// </summary>
        private async Task<UpdateCheckResult> FetchUpdateCheckResultAsync(string owner, string repo, CancellationToken cancellationToken)
        {
            // Get the current version
            string currentVersion = GetCurrentVersion();
            _logger.LogInformation("Current version: {Version}", currentVersion);

            // Get the latest release from GitHub
            GitHubRelease? latestRelease = null;

            if (_releaseReader != null)
            {
                // Use the dedicated release reader if available
                latestRelease = await _releaseReader.GetLatestReleaseAsync(owner, repo, cancellationToken);
            }
            else
            {
                // Fallback to direct API call
                latestRelease = await GetLatestReleaseForRepoAsync(owner, repo, true, cancellationToken);
            }

            if (latestRelease == null)
            {
                _logger.LogWarning("No release found for {Owner}/{Repo}", owner, repo);
                return new UpdateCheckResult
                {
                    IsUpdateAvailable = false,
                    ErrorMessages = { "No release found" }
                };
            }

            // Extract the version using the dedicated method - ensures consistency
            string latestVersion = ExtractVersion(latestRelease);
            _logger.LogInformation("Latest version: {Version} (from tag: {Tag})",
                latestVersion, latestRelease.TagName ?? "unknown");

            // Compare versions using the injected comparator service
            bool updateAvailable = false;
            try
            {
                int comparisonResult = _versionComparator.Compare(currentVersion, latestVersion);
                updateAvailable = comparisonResult < 0;

                _logger.LogInformation("Version comparison result: {Result} -> Update available: {IsAvailable}",
                    comparisonResult, updateAvailable);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error comparing versions {CurrentVersion} and {LatestVersion}",
                    currentVersion, latestVersion);
                // Default to not available in case of error
                updateAvailable = false;
            }

            return new UpdateCheckResult
            {
                IsUpdateAvailable = updateAvailable,
                LatestRelease = latestRelease
            };
        }

        /// <summary>
        /// Extracts a semantic version from a GitHub release
        /// </summary>
        private string ExtractVersion(GitHubRelease release)
        {
            // Try multiple sources for version information in order of preference

            // 1. Try tag name with "v" prefix removed if present
            if (!string.IsNullOrEmpty(release.TagName))
            {
                var version = release.TagName.TrimStart('v');
                if (IsValidVersionFormat(version))
                {
                    return version;
                }
            }

            // 2. Try Version property if available
            if (!string.IsNullOrEmpty(release.Version))
            {
                return release.Version;
            }

            // 3. Try to extract from Name if it contains version-like patterns
            if (!string.IsNullOrEmpty(release.Name))
            {
                // Look for patterns like "Release 1.2.3" or "v1.2.3" in the name
                var versionMatch = System.Text.RegularExpressions.Regex.Match(
                    release.Name,
                    @"v?(\d+\.\d+\.\d+(?:\.\d+)?(?:-[a-zA-Z0-9\.-]+)?)"
                );

                if (versionMatch.Success)
                {
                    return versionMatch.Groups[1].Value;
                }
            }

            // 4. Try extraction from body text as last resort
            if (!string.IsNullOrEmpty(release.Body))
            {
                var versionMatch = System.Text.RegularExpressions.Regex.Match(
                    release.Body,
                    @"[vV]ersion\s*:?\s*v?(\d+\.\d+\.\d+(?:\.\d+)?(?:-[a-zA-Z0-9\.-]+)?)"
                );

                if (versionMatch.Success)
                {
                    return versionMatch.Groups[1].Value;
                }
            }

            // Default: use publish date as fallback (in format YYYY.MM.DD)
            if (release.PublishedAt != default)
            {
                return $"{release.PublishedAt.Year}.{release.PublishedAt.Month}.{release.PublishedAt.Day}";
            }

            // Ultimate fallback
            return "0.0.0";
        }

        /// <summary>
        /// Validates if a string is in a valid version format
        /// </summary>
        private bool IsValidVersionFormat(string version)
        {
            // Simple validation for semantic version-like format
            return System.Text.RegularExpressions.Regex.IsMatch(
                version,
                @"^\d+\.\d+\.\d+(?:\.\d+)?(?:-[a-zA-Z0-9\.-]+)?$"
            );
        }

        /// <summary>
        /// Gets the latest release from the default repository
        /// </summary>
        public async Task<GitHubRelease?> GetLatestReleaseAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var settings = await GetRepositorySettingsAsync();
                if (settings == null)
                {
                    _logger.LogWarning("No repository settings found for getting latest release");
                    return null;
                }

                return await GetLatestReleaseForRepoAsync(settings.RepoOwner, settings.RepoName, false, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting latest release");
                return null;
            }
        }

        /// <summary>
        /// Gets the latest release for a specific repository
        /// </summary>
        public async Task<GitHubRelease?> GetLatestReleaseForRepoAsync(
            string owner,
            string repo,
            bool forceFresh = false,
            CancellationToken cancellationToken = default)
        {
            try
            {
                string cacheKey = $"latest_release_{owner}_{repo}";

                // If we're forcing fresh data or we don't have a release reader, fetch directly
                if (forceFresh || _releaseReader == null)
                {
                    // Direct API implementation
                    var requestUri = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";
                    var request = new HttpRequestMessage(HttpMethod.Get, requestUri);

                    // Set GitHub API headers
                    request.Headers.UserAgent.ParseAdd("GenHub");
                    request.Headers.Accept.ParseAdd("application/vnd.github.v3+json");

                    // Log the request
                    _logger.LogDebug("Sending GitHub API request to: {Uri}", requestUri);

                    var response = await _httpClient.SendAsync(request, cancellationToken);

                    // Log response status
                    _logger.LogDebug("GitHub API response: {StatusCode}", response.StatusCode);

                    if (!response.IsSuccessStatusCode)
                    {
                        string errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                        _logger.LogWarning("Failed to fetch latest release: HTTP {StatusCode}. Response: {Response}",
                            response.StatusCode, errorContent);
                        return null;
                    }

                    using var content = await response.Content.ReadAsStreamAsync(cancellationToken);
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        // Add converter for dates
                        Converters = {
                            new System.Text.Json.Serialization.JsonStringEnumConverter(),
                            new System.Text.Json.Serialization.JsonDateTimeConverter()
                        }
                    };

                    var release = await JsonSerializer.DeserializeAsync<GitHubRelease>(content, options, cancellationToken);

                    // Basic validation and enrichment
                    if (release != null)
                    {
                        _logger.LogInformation("Successfully fetched release: {Name}, Tag: {Tag}",
                            release.Name, release.TagName);

                        // Set a version even if it wasn't in the response
                        if (string.IsNullOrEmpty(release.Version))
                        {
                            release.Version = ExtractVersion(release);
                            _logger.LogDebug("Extracted version {Version} from release", release.Version);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Deserialized release is null");
                    }

                    return release;
                }
                else
                {
                    // Use the release reader
                    var release = await _releaseReader.GetLatestReleaseAsync(owner, repo, cancellationToken);

                    // Ensure version is populated
                    if (release != null && string.IsNullOrEmpty(release.Version))
                    {
                        release.Version = ExtractVersion(release);
                    }

                    return release;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting latest release for {Owner}/{Repo}", owner, repo);
                return null;
            }
        }

        /// <summary>
        /// Gets the repository settings used for update checking
        /// </summary>
        public async Task<GitHubRepoSettings> GetRepositorySettingsAsync()
        {
            try
            {
                // Use the repository manager to get the current repository
                var settings = _repositoryManager.GetCurrentRepository();

                if (settings == null)
                {
                    _logger.LogInformation("No custom update repository settings found, using default");

                    // Create default settings
                    settings = new GitHubRepoSettings
                    {
                        RepoOwner = "undead2146",
                        RepoName = "GenHub",
                        DisplayName = "GenHub (Default)"
                    };

                    // Save the default
                    _repositoryManager.SaveCurrentRepository(settings);
                }

                return settings;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting repository settings");
                // Must return a non-null value to satisfy interface contract
                return new GitHubRepoSettings
                {
                    RepoOwner = "undead2146",
                    RepoName = "GenHub",
                    DisplayName = "GenHub (Default)"
                };
            }
        }

        /// <summary>
        /// Saves the repository settings used for update checking
        /// </summary>
        public async Task SaveRepositorySettingsAsync(GitHubRepoSettings settings)
        {
            try
            {
                // Use the repository manager to save the current repository
                _repositoryManager.SaveCurrentRepository(settings);

                _logger.LogInformation("Saved update repository settings: {Owner}/{Repo}",
                    settings.RepoOwner, settings.RepoName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving repository settings");
            }
        }

        /// <summary>
        /// Updates the application to the specified release
        /// </summary>
        public async Task UpdateApplicationAsync(GitHubRelease release, IProgress<UpdateProgress>? progressReporter = null, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Starting application update to version: {Version}", release.Version);

                // Report initial progress
                progressReporter?.Report(new UpdateProgress
                {
                    Status = "Starting update...",
                    PercentageCompleted = 0.0
                });

                // Here we would implement platform-specific update logic
                // For now, we delegate to InitiateUpdateAsync
                await InitiateUpdateAsync(release, progressReporter, cancellationToken);

                progressReporter?.Report(new UpdateProgress
                {
                    Status = "Update completed",
                    PercentageCompleted = 1.0
                });

                _logger.LogInformation("Application update completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating application");
                progressReporter?.Report(new UpdateProgress
                {
                    Status = $"Update failed: {ex.Message}",
                    PercentageCompleted = 0.0
                });
                throw;
            }
        }

        /// <summary>
        /// Initiates the update installation process
        /// </summary>
        public async Task InitiateUpdateAsync(GitHubRelease release, IProgress<UpdateProgress>? progressReporter = null, CancellationToken cancellationToken = default)
        {
            try
            {
                // Perform some validation before starting
                if (release == null)
                    throw new ArgumentNullException(nameof(release));

                _logger.LogInformation("Initiating update to version: {Version}", release.Version);

                if (_updateInstaller == null)
                {
                    _logger.LogError("No update installer is available");

                    // Report an error
                    progressReporter?.Report(new UpdateProgress
                    {
                        Status = "Error: Update installer is not available",
                        PercentageCompleted = 0.0,
                        IsInProgress = false,
                        IsSuccessful = false
                    });

                    return;
                }

                // Enhanced logging for installer type
                var installerTypeName = _updateInstaller.GetType().FullName;
                _logger.LogInformation("Update installer type: {InstallerType}", installerTypeName);

                // If it's the default installer, log a warning
                if (installerTypeName.Contains("DefaultUpdateInstaller"))
                {
                    _logger.LogWarning("Using DefaultUpdateInstaller (simulation only). Platform-specific installer was not loaded.");
                }

                // Report progress if a progress handler is provided
                progressReporter?.Report(new UpdateProgress
                {
                    Status = "Starting update process...",
                    PercentageCompleted = 0.1,
                    IsInProgress = true,
                    Message = $"Using {installerTypeName}"
                });

                // Delegate to the update installer
                await _updateInstaller.InstallUpdateAsync(release, progressReporter, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initiating update");

                // Report error to the progress handler
                progressReporter?.Report(new UpdateProgress
                {
                    Status = $"Update failed: {ex.Message}",
                    PercentageCompleted = 0.0,
                    IsInProgress = false,
                    IsSuccessful = false
                });

                throw;
            }
        }

        /// <summary>
        /// Gets a list of all saved repositories
        /// </summary>
        public async Task<IEnumerable<GitHubRepoSettings>> GetSavedRepositoriesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Get repositories directly from repository manager
                var repositories = _repositoryManager.GetRepositories();

                // If no repositories are found, return a list with just the default repository
                if (repositories == null || !repositories.Any())
                {
                    var defaultSettings = new GitHubRepoSettings
                    {
                        RepoOwner = "undead2146",
                        RepoName = "GenHub",
                        DisplayName = "GenHub (Default)"
                    };

                    return new List<GitHubRepoSettings> { defaultSettings };
                }

                return repositories;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting saved repositories");

                // Return default repository as fallback
                var defaultSettings = new GitHubRepoSettings
                {
                    RepoOwner = "undead2146",
                    RepoName = "GenHub",
                    DisplayName = "GenHub (Default)"
                };

                return new List<GitHubRepoSettings> { defaultSettings };
            }
        }

        /// <summary>
        /// Saves a list of repositories
        /// </summary>
        public async Task SaveRepositoriesAsync(IEnumerable<GitHubRepoSettings> repositories, CancellationToken cancellationToken = default)
        {
            try
            {
                // Save repositories through the repository manager
                _repositoryManager.SaveRepositories(repositories);
                _logger.LogInformation("Saved {Count} repositories to shared settings", repositories.Count());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving repositories");
                throw;
            }
        }
    }
}
