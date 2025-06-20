using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using GenHub.Core.Interfaces.GitHub;
using GenHub.Core.Interfaces.Caching;
using GenHub.Core.Models.GitHub;
using GenHub.Core.Models;

namespace GenHub.Features.GitHub.Services
{
    /// <summary>
    /// Service for interacting with GitHub releases
    /// </summary>
    public class GitHubReleaseReader : IGitHubReleaseReader
    {
        private readonly ILogger<GitHubReleaseReader> _logger;
        private readonly IGitHubApiClient _gitHubApiClient;
        private readonly ICacheService _cacheService;
        
        // Release cache duration (4 hours)
        private static readonly TimeSpan ReleaseCacheDuration = TimeSpan.FromHours(4);
        
        /// <summary>
        /// Creates a new instance of GitHubReleaseReader
        /// </summary>
        public GitHubReleaseReader(
            ILogger<GitHubReleaseReader> logger,
            IGitHubApiClient gitHubApiClient,
            ICacheService cacheService)
        {
            _logger = logger;
            _gitHubApiClient = gitHubApiClient;
            _cacheService = cacheService;
        }
        
        /// <summary>
        /// Gets all releases for a repository
        /// </summary>
        public async Task<IEnumerable<GitHubRelease>> GetReleasesAsync(
            string owner, 
            string repo, 
            int page = 1,
            int perPage = 30,
            bool includePrereleases = true,
            CancellationToken cancellationToken = default)
        {
            try 
            {
                string cacheKey = $"github_releases_{owner}_{repo}_{page}_{perPage}_{includePrereleases}";
                
                // Directly get GitHub releases from the API client
                var releases = await _cacheService.GetOrFetchAsync<IEnumerable<GitHubRelease>>(
                    cacheKey,
                    async () => 
                    {
                        var endpoint = $"repos/{owner}/{repo}/releases?page={page}&per_page={perPage}";
                        var apiReleases = await _gitHubApiClient.GetAsync<IEnumerable<GitHubRelease>>(endpoint, cancellationToken);
                        
                        // Filter prereleases if needed
                        var filteredReleases = includePrereleases 
                            ? apiReleases 
                            : apiReleases?.Where(r => !r.Prerelease);
                        
                        // Ensure repository information is set on each release
                        if (filteredReleases != null)
                        {
                            foreach (var release in filteredReleases)
                            {
                                release.RepositoryInfo = new GitHubRepository
                                {
                                    RepoOwner = owner,
                                    RepoName = repo,
                                    DisplayName = $"{owner}/{repo}"
                                };
                            }
                        }
                        
                        return filteredReleases;
                    },
                    ReleaseCacheDuration,
                    cancellationToken);

                return releases ?? Enumerable.Empty<GitHubRelease>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting releases for {Owner}/{Repo}", owner, repo);
                return Enumerable.Empty<GitHubRelease>();
            }
        }
        
        /// <summary>
        /// Gets a specific release by tag name
        /// </summary>
        public async Task<GitHubRelease?> GetReleaseByTagAsync(string owner, string repo, string tag, CancellationToken cancellationToken = default)
        {
            try
            {
                string cacheKey = $"github_release_{owner}_{repo}_{tag}";
                
                // Try to get directly from the API using the tag endpoint
                return await _cacheService.GetOrFetchAsync<GitHubRelease?>(
                    cacheKey,
                    async () =>
                    {
                        var endpoint = $"repos/{owner}/{repo}/releases/tags/{tag}";
                        var release = await _gitHubApiClient.GetAsync<GitHubRelease>(endpoint, cancellationToken);
                        
                        if (release != null)
                        {
                            release.RepositoryInfo = new GitHubRepository
                            {
                                RepoOwner = owner,
                                RepoName = repo,
                                DisplayName = $"{owner}/{repo}"
                            };
                        }
                        
                        return release;
                    },
                    ReleaseCacheDuration,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting release by tag {Tag} for {Owner}/{Repo}", tag, owner, repo);
                
                // Try fallback method - get all releases and filter
                try
                {
                    var releases = await GetReleasesAsync(owner, repo, cancellationToken: cancellationToken);
                    return releases.FirstOrDefault(r => 
                        r.TagName.Equals(tag, StringComparison.OrdinalIgnoreCase) || 
                        r.TagName.Equals($"v{tag}", StringComparison.OrdinalIgnoreCase));
                }
                catch
                {
                    return null;
                }
            }
        }
        
        /// <summary>
        /// Gets a specific release asset by ID
        /// </summary>
        public async Task<GitHubReleaseAsset?> GetReleaseAssetAsync(string owner, string repo, long assetId, CancellationToken cancellationToken = default)
        {
            var releases = await GetReleasesAsync(owner, repo, cancellationToken: cancellationToken);
            
            foreach (var release in releases)
            {
                if (release.Assets != null)
                {
                    foreach (var asset in release.Assets)
                    {
                        if (asset.Id == assetId)
                        {
                            return asset;
                        }
                    }
                }
            }
            
            return null;
        }

        /// <summary>
        /// Gets the latest release for a repository
        /// </summary>
        public async Task<GitHubRelease?> GetLatestReleaseAsync(string owner, string repo, CancellationToken cancellationToken = default)
        {
            try
            {
                string cacheKey = $"github_latest_release_{owner}_{repo}";
                
                // Try to get from cache or fetch from API
                return await _cacheService.GetOrFetchAsync<GitHubRelease?>(
                    cacheKey,
                    async () => 
                    {
                        var endpoint = $"repos/{owner}/{repo}/releases/latest";
                        var release = await _gitHubApiClient.GetAsync<GitHubRelease>(endpoint, cancellationToken);
                        
                        if (release != null)
                        {
                            release.RepositoryInfo = new GitHubRepository
                            {
                                RepoOwner = owner,
                                RepoName = repo,
                                DisplayName = $"{owner}/{repo}"
                            };
                        }
                        
                        return release;
                    },
                    ReleaseCacheDuration,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting latest release for {Owner}/{Repo}", owner, repo);
                
                // Try fallback - get all releases and pick the first one
                try
                {
                    var releases = await GetReleasesAsync(owner, repo, cancellationToken: cancellationToken);
                    return releases.FirstOrDefault();
                }
                catch
                {
                    return null;
                }
            }
        }
    }
}
