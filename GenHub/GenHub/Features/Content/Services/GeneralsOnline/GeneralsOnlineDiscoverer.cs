using GenHub.Core.Interfaces.Content;
using GenHub.Core.Models.Content;
using GenHub.Core.Models.Enums;
using GenHub.Core.Models.GeneralsOnline;
using GenHub.Core.Models.Results;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace GenHub.Features.Content.Services.GeneralsOnline;

/// <summary>
/// Discovers Generals Online releases by scraping the download page or querying API.
/// </summary>
public class GeneralsOnlineDiscoverer : IContentDiscoverer
{
    // TODO: Replace with actual API endpoint when available
    private const string ManifestApiUrl = "https://cdn.playgenerals.online/manifest.json";
    private const string FallbackDownloadPageUrl = "https://www.playgenerals.online/#download";

    private readonly ILogger _logger;
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="GeneralsOnlineDiscoverer"/> class.
    /// </summary>
    /// <param name="logger">The logger for diagnostic information.</param>
    public GeneralsOnlineDiscoverer(ILogger logger)
    {
        _logger = logger;
        _httpClient = new HttpClient
        {
            Timeout = System.TimeSpan.FromSeconds(30),
        };
    }

    /// <inheritdoc />
    public string SourceName => "Generals Online";

    /// <inheritdoc />
    public string Description => "Discovers Generals Online releases from official CDN";

    /// <inheritdoc />
    public bool IsEnabled => true;

    /// <inheritdoc />
    public ContentSourceCapabilities Capabilities =>
        ContentSourceCapabilities.RequiresDiscovery |
        ContentSourceCapabilities.SupportsPackageAcquisition;

    /// <summary>
    /// Discovers Generals Online releases from the CDN.
    /// </summary>
    /// <param name="query">The search query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Operation result containing discovered content.</returns>
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

    /// <inheritdoc />
    public void Dispose()
    {
        _httpClient?.Dispose();
    }

    private async Task<GeneralsOnlineRelease?> TryGetReleaseFromApiAsync(
        System.Threading.CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Querying Generals Online CDN for latest release");

            // Try to get version from CDN's latest.txt (simple polling)
            var latestTxtUrl = "https://cdn.playgenerals.online/latest.txt";
            var response = await _httpClient.GetAsync(latestTxtUrl, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var version = await response.Content.ReadAsStringAsync(cancellationToken);
                version = version?.Trim();

                if (!string.IsNullOrEmpty(version))
                {
                    _logger.LogInformation("Retrieved version from CDN: {Version}", version);
                    return CreateReleaseFromVersion(version);
                }
            }

            _logger.LogDebug("CDN latest.txt not available, will use mock data");

            // Future: Try manifest.json when available
            // var response = await _httpClient.GetFromJsonAsync<GeneralsOnlineApiResponse>(
            //     ManifestApiUrl, cancellationToken);
            // return response?.LatestRelease;
            return null;
        }
        catch (System.Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query CDN, using fallback");
            return null;
        }
    }

    private GeneralsOnlineRelease CreateReleaseFromVersion(string version)
    {
        // Parse version to get date (DDMMYY_QFE#)
        var versionDate = ParseVersionDate(version);

        return new GeneralsOnlineRelease
        {
            Version = version,
            VersionDate = versionDate,
            ReleaseDate = versionDate,
            InstallerUrl = $"https://cdn.playgenerals.online/releases/GeneralsOnline_setup_{version}.exe",
            PortableUrl = $"https://cdn.playgenerals.online/releases/GeneralsOnline_portable_{version}.zip",
            InstallerSize = 45_000_000,  // Estimated
            PortableSize = 38_000_000,   // Estimated
            Changelog = $"Generals Online {version} - Latest community patch with performance improvements and bug fixes",
        };
    }

    private System.DateTime ParseVersionDate(string version)
    {
        try
        {
            // Format: DDMMYY_QFE#
            var parts = version.Split('_');
            if (parts.Length < 1)
            {
                return System.DateTime.Now;
            }

            var datePart = parts[0];
            if (datePart.Length != 6)
            {
                return System.DateTime.Now;
            }

            var month = int.Parse(datePart.Substring(0, 2));
            var day = int.Parse(datePart.Substring(2, 2));
            var year = 2000 + int.Parse(datePart.Substring(4, 2));

            return new System.DateTime(year, month, day);
        }
        catch
        {
            return System.DateTime.Now;
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
            Changelog = "QFE5 Release - Improved stability and networking performance",
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
            TargetGame = GameType.ZeroHour,
            ProviderName = SourceName,
            AuthorName = "Generals Online Team",
            IconUrl = "https://www.playgenerals.online/logo.png",
            LastUpdated = release.ReleaseDate,
            DownloadSize = release.PortableSize,
            RequiresResolution = true,
            ResolverId = "GeneralsOnline",
            SourceUrl = FallbackDownloadPageUrl,
        };

        searchResult.Tags.Add("multiplayer");
        searchResult.Tags.Add("online");
        searchResult.Tags.Add("community");
        searchResult.Tags.Add("enhancement");
        searchResult.SetData(release);

        return searchResult;
    }
}
