using GenHub.Core.Interfaces.Content;
using GenHub.Core.Models.Content;
using GenHub.Core.Models.Enums;
using GenHub.Core.Models.Manifest;
using GenHub.Core.Models.Results;
using GenHub.Plugins.GeneralsOnline.Models;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace GenHub.Plugins.GeneralsOnline;

/// <summary>
/// Discovers Generals Online releases by scraping the download page or querying API.
/// </summary>
public class GeneralsOnlineDiscoverer : IContentDiscoverer
{
    private readonly ILogger _logger;
    private readonly HttpClient _httpClient;
    
    // TODO: Replace with actual API endpoint when available
    private const string ManifestApiUrl = "https://cdn.playgenerals.online/manifest.json";
    private const string FallbackDownloadPageUrl = "https://www.playgenerals.online/#download";
    
    public GeneralsOnlineDiscoverer(ILogger logger)
    {
        _logger = logger;
        _httpClient = new HttpClient
        {
            Timeout = System.TimeSpan.FromSeconds(30)
        };
    }

    public string SourceName => "Generals Online";
    public string Description => "Discovers Generals Online releases from official CDN";
    public bool IsEnabled => true;
    public ContentSourceCapabilities Capabilities =>
        ContentSourceCapabilities.RequiresDiscovery |
        ContentSourceCapabilities.SupportsVersioning;

    public async Task<OperationResult<System.Collections.Generic.IEnumerable<ContentSearchResult>>> DiscoverAsync(
        ContentSearchQuery query,
        System.Threading.CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Discovering Generals Online releases");

        try
        {
            // Try API first, fall back to mock if unavailable
            var release = await TryGetReleaseFromApiAsync(cancellationToken) 
                          ?? await GetMockReleaseAsync();

            if (release == null)
            {
                return OperationResult<System.Collections.Generic.IEnumerable<ContentSearchResult>>.CreateFailure(
                    "Could not discover Generals Online releases");
            }

            // Filter by search query if provided
            if (!string.IsNullOrWhiteSpace(query.SearchTerm) &&
                !release.Version.Contains(query.SearchTerm, System.StringComparison.OrdinalIgnoreCase) &&
                !"generals online".Contains(query.SearchTerm, System.StringComparison.OrdinalIgnoreCase))
            {
                return OperationResult<System.Collections.Generic.IEnumerable<ContentSearchResult>>.CreateSuccess(
                    System.Linq.Enumerable.Empty<ContentSearchResult>());
            }

            var searchResult = CreateSearchResult(release);

            return OperationResult<System.Collections.Generic.IEnumerable<ContentSearchResult>>.CreateSuccess(
                new[] { searchResult });
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Failed to discover Generals Online releases");
            return OperationResult<System.Collections.Generic.IEnumerable<ContentSearchResult>>.CreateFailure(
                $"Discovery failed: {ex.Message}");
        }
    }

    private async Task<GeneralsOnlineRelease?> TryGetReleaseFromApiAsync(
        System.Threading.CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Querying Generals Online API: {Url}", ManifestApiUrl);
            
            // TODO: Implement when API is available
            // var response = await _httpClient.GetFromJsonAsync<GeneralsOnlineApiResponse>(
            //     ManifestApiUrl, cancellationToken);
            // return response?.LatestRelease;
            
            _logger.LogInformation("API not yet available, using fallback");
            return null;
        }
        catch (System.Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query API, using fallback");
            return null;
        }
    }

    private async Task<GeneralsOnlineRelease> GetMockReleaseAsync()
    {
        // Mock data for testing until API is ready
        _logger.LogInformation("Using mock release data for development");
        
        return await System.Threading.Tasks.Task.FromResult(new GeneralsOnlineRelease
        {
            Version = "101525_QFE5",
            VersionDate = new System.DateTime(2025, 10, 15),
            ReleaseDate = new System.DateTime(2025, 10, 15),
            InstallerUrl = "https://cdn.playgenerals.online/GeneralsOnline_setup_101525_QFE5.exe",
            PortableUrl = "https://cdn.playgenerals.online/GeneralsOnline_portable_101525_QFE5.zip",
            InstallerSize = 45_000_000,
            PortableSize = 38_000_000,
            Changelog = "QFE5 Release - Improved stability and networking performance"
        });
    }

    private ContentSearchResult CreateSearchResult(GeneralsOnlineRelease release)
    {
        var searchResult = new ContentSearchResult
        {
            Id = $"GeneralsOnline_{release.Version}",
            Name = "Generals Online",
            Description = "Community-driven multiplayer service for C&C Generals Zero Hour. " +
                         "Features 60Hz tick rate, automatic updates, encrypted traffic, and improved stability.",
            Version = release.Version,
            ContentType = ContentType.Mod,
            TargetGame = GameType.CNCGeneralsZeroHour,
            ProviderName = SourceName,
            AuthorName = "Generals Online Team",
            IconUrl = "https://www.playgenerals.online/logo.png",
            LastUpdated = release.ReleaseDate,
            DownloadSize = release.PortableSize,
            RequiresResolution = true,
            ResolverId = "GeneralsOnline",
            SourceUrl = FallbackDownloadPageUrl
        };

        searchResult.Tags.Add("multiplayer");
        searchResult.Tags.Add("online");
        searchResult.Tags.Add("community");
        searchResult.Tags.Add("enhancement");
        searchResult.SetData(release);

        return searchResult;
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
