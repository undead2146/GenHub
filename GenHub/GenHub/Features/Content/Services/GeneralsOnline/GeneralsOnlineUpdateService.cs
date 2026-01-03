using GenHub.Core.Constants;
using GenHub.Core.Interfaces.Manifest;
using GenHub.Core.Interfaces.Providers;
using GenHub.Core.Models.Results.Content;
using Microsoft.Extensions.Logging;
using System;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace GenHub.Features.Content.Services.GeneralsOnline;

/// <summary>
/// Background service for checking Generals Online updates.
/// Polls CDN for new releases and notifies when updates are available.
/// Uses data-driven configuration from provider.json for endpoints.
/// </summary>
public class GeneralsOnlineUpdateService(
    ILogger<GeneralsOnlineUpdateService> logger,
    IContentManifestPool manifestPool,
    IHttpClientFactory httpClientFactory,
    IProviderDefinitionLoader providerLoader) : ContentUpdateServiceBase(logger)
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient(GeneralsOnlineConstants.PublisherType);

    /// <inheritdoc />
    public override void Dispose()
    {
        _httpClient?.Dispose();
        base.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    protected override string ServiceName => GeneralsOnlineConstants.ContentName;

    /// <inheritdoc />
    protected override TimeSpan UpdateCheckInterval =>
        TimeSpan.FromHours(GeneralsOnlineConstants.UpdateCheckIntervalHours);

    /// <inheritdoc />
    public override async Task<ContentUpdateCheckResult>
        CheckForUpdatesAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Checking for Generals Online updates");

        try
        {
            // Get current installed version
            var currentVersion = await GetInstalledVersionAsync(cancellationToken);

            // Get latest version from CDN
            var latestVersion = await GetLatestVersionFromCdnAsync(cancellationToken);

            if (string.IsNullOrEmpty(latestVersion))
            {
                logger.LogWarning("Could not retrieve latest version from CDN");
                return ContentUpdateCheckResult.CreateFailure(
                    "Could not retrieve latest version from CDN",
                    currentVersion);
            }

            var updateAvailable = IsNewerVersion(latestVersion, currentVersion);

            if (updateAvailable)
            {
                return ContentUpdateCheckResult.CreateUpdateAvailable(
                    latestVersion: latestVersion,
                    currentVersion: currentVersion);
            }

            return ContentUpdateCheckResult.CreateNoUpdateAvailable(
                currentVersion: currentVersion,
                latestVersion: latestVersion);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to check for Generals Online updates");
            throw;
        }
    }

    private async Task<string?> GetInstalledVersionAsync(CancellationToken cancellationToken)
    {
        try
        {
            var manifests = await manifestPool.GetAllManifestsAsync(cancellationToken);
            if (!manifests.Success || manifests.Data == null)
            {
                return null;
            }

            var goManifest = manifests.Data.FirstOrDefault(m =>
                m.Publisher?.PublisherType?.Equals(GeneralsOnlineConstants.PublisherType, StringComparison.OrdinalIgnoreCase) == true);

            return goManifest?.Version;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get installed Generals Online version");
            return null;
        }
    }

    private async Task<string?> GetLatestVersionFromCdnAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Get provider definition
            var provider = providerLoader.GetProvider(GeneralsOnlineConstants.PublisherType);
            if (provider == null)
            {
                logger.LogError("Provider definition not found for {ProviderId}", GeneralsOnlineConstants.PublisherType);
                return null;
            }

            var latestVersionUrl = provider.Endpoints.GetEndpoint(ProviderEndpointConstants.LatestVersionUrl);
            if (string.IsNullOrEmpty(latestVersionUrl))
            {
                // Fallback to standard endpoint name lookup
                latestVersionUrl = provider.Endpoints.GetEndpoint("latestVersionUrl");
            }

            if (string.IsNullOrEmpty(latestVersionUrl))
            {
                logger.LogError("latestVersionUrl not configured in provider definition (checked both 'custom.latestVersionUrl' and 'latestVersionUrl')");
                return null;
            }

            // Try to get version from latest.txt with retries
            HttpResponseMessage? response = null;
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    response = await _httpClient.GetAsync(latestVersionUrl, cancellationToken);
                    if (response.IsSuccessStatusCode)
                    {
                        break;
                    }
                }
                catch (Exception ex) when (i < 2)
                {
                    logger.LogWarning(ex, "Attempt {Count} to fetch latest.txt failed", i + 1);
                }

                if (i < 2)
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, i + 1)), cancellationToken);
                }
            }

            if (response != null && response.IsSuccessStatusCode)
            {
                var version = await response.Content.ReadAsStringAsync(cancellationToken);
                return version?.Trim();
            }

            logger.LogWarning("latest.txt not available, falling back to manifest.json");

            // Fallback: get version from manifest.json (catalogUrl)
            var catalogUrl = provider.Endpoints.GetEndpoint(ProviderEndpointConstants.CatalogUrl);
            if (!string.IsNullOrEmpty(catalogUrl))
            {
                var manifestResponse = await _httpClient.GetAsync(catalogUrl, cancellationToken);
                if (manifestResponse.IsSuccessStatusCode)
                {
                    var json = await manifestResponse.Content.ReadAsStringAsync(cancellationToken);

                    // Simple JSON parsing for "version" field
                    var versionStart = json.IndexOf("\"version\":", StringComparison.OrdinalIgnoreCase);
                    if (versionStart >= 0)
                    {
                        versionStart += 10; // length of "version":
                        var versionEnd = json.IndexOf('"', versionStart);
                        if (versionEnd > versionStart)
                        {
                            var version = json[versionStart..versionEnd].Trim().Trim('"');
                            logger.LogInformation("Retrieved latest version from manifest.json: {Version}", version);
                            return version;
                        }
                    }
                }
            }

            logger.LogWarning("Failed to retrieve latest version from both latest.txt and manifest.json");
            return null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get latest version from CDN");
            return null;
        }
    }

    private bool IsNewerVersion(string latestVersion, string? currentVersion)
    {
        if (string.IsNullOrEmpty(currentVersion))
        {
            return true; // Any version is newer than nothing
        }

        // Parse DDMMYY_QFE# format
        var latest = ParseVersion(latestVersion);
        var current = ParseVersion(currentVersion);

        if (latest == null || current == null)
        {
            return false;
        }

        // Compare date first
        if (latest.Value.Date > current.Value.Date)
        {
            return true;
        }

        if (latest.Value.Date == current.Value.Date)
        {
            // Same date, compare QFE number
            return latest.Value.Qfe > current.Value.Qfe;
        }

        return false;
    }

    private (DateTime Date, int Qfe)? ParseVersion(string version)
    {
        try
        {
            // Format: DDMMYY_QFE#
            var parts = version.Split('_');
            if (parts.Length != 2)
            {
                return null;
            }

            var datePart = parts[0];
            var qfePart = parts[1].Replace("QFE", string.Empty);

            if (datePart.Length != 6 || !int.TryParse(qfePart, out var qfe))
            {
                return null;
            }

            if (!DateTime.TryParseExact(
                datePart,
                GeneralsOnlineConstants.VersionDateFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date))
            {
                logger.LogWarning("Failed to parse version date: {DatePart}", datePart);
                return null;
            }

            return (date, qfe);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse version: {Version}", version);
            return null;
        }
    }
}
