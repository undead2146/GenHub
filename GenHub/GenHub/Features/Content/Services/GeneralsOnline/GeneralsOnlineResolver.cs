using GenHub.Core.Constants;
using GenHub.Core.Interfaces.Content;
using GenHub.Core.Interfaces.Providers;
using GenHub.Core.Models.Enums;
using GenHub.Core.Models.GeneralsOnline;
using GenHub.Core.Models.Manifest;
using GenHub.Core.Models.Results;
using GenHub.Core.Models.Results.Content;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GenHub.Features.Content.Services.GeneralsOnline;

/// <summary>
/// Resolves Generals Online search results into ContentManifests with download URLs.
/// Creates the initial manifest structure; post-extraction processing is handled by the factory.
/// </summary>
public class GeneralsOnlineResolver(
    GeneralsOnlineManifestFactory manifestFactory,
    ILogger<GeneralsOnlineResolver> logger,
    IProviderDefinitionLoader? providerLoader = null) : IContentResolver
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GeneralsOnlineResolver"/> class without an explicit provider loader.
    /// </summary>
    /// <param name="manifestFactory">Manifest factory for creating manifests.</param>
    /// <param name="logger">Logger instance.</param>
    public GeneralsOnlineResolver(
        GeneralsOnlineManifestFactory manifestFactory,
        ILogger<GeneralsOnlineResolver> logger)
        : this(manifestFactory, logger, null)
    {
    }

    /// <inheritdoc />
    public string ResolverId => GeneralsOnlineConstants.ResolverId;

    /// <summary>
    /// Resolves a Generals Online search result into a content manifest.
    /// Creates the 30Hz variant manifest with download URL; deliverer will handle download.
    /// </summary>
    /// <param name="searchResult">The search result to resolve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Operation result containing the resolved manifest.</returns>
    public Task<OperationResult<ContentManifest>> ResolveAsync(
        ContentSearchResult searchResult,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Resolving Generals Online manifest for: {Version}", searchResult.Version);

        try
        {
            var release = searchResult.GetData<GeneralsOnlineRelease>();
            if (release == null)
            {
                if (!string.IsNullOrWhiteSpace(searchResult.Version))
                {
                    logger.LogInformation(
                        "Release payload missing from search result; reconstructing release metadata for version {Version}",
                        searchResult.Version);

                    var portableUrl = searchResult.SelectedDownloadUrl;
                    if (string.IsNullOrWhiteSpace(portableUrl) ||
                        !portableUrl.EndsWith(GeneralsOnlineConstants.PortableExtension, StringComparison.OrdinalIgnoreCase))
                    {
                        var provider = providerLoader?.GetProvider(PublisherTypeConstants.GeneralsOnline);
                        var releasesUrl = provider?.Endpoints.GetEndpoint("releasesUrl") ?? "https://cdn.playgenerals.online/releases";
                        portableUrl = $"{releasesUrl}/{GeneralsOnlineConstants.PortableFilePrefix}{searchResult.Version}{GeneralsOnlineConstants.PortableExtension}";
                    }

                    var versionDate = searchResult.LastUpdated ?? ParseVersionDate(searchResult.Version) ?? DateTime.UtcNow;

                    release = new GeneralsOnlineRelease
                    {
                        Version = searchResult.Version,
                        VersionDate = versionDate,
                        ReleaseDate = searchResult.LastUpdated ?? versionDate,
                        PortableUrl = portableUrl,
                        PortableSize = searchResult.DownloadSize > 0 ? searchResult.DownloadSize : null,
                        Changelog = !string.IsNullOrWhiteSpace(searchResult.Description) ? searchResult.Description : $"Generals Online {searchResult.Version}",
                    };
                }
                else
                {
                    return Task.FromResult(OperationResult<ContentManifest>.CreateFailure(
                        "Release information not found in search result"));
                }
            }

            var manifests = manifestFactory.CreateManifests(release);
            if (manifests.FirstOrDefault() is not { } primaryManifest)
            {
                return Task.FromResult(OperationResult<ContentManifest>.CreateFailure(
                    "Failed to create manifest from release"));
            }

            logger.LogInformation(
                "Successfully resolved Generals Online manifest ({Variant}) with download URL: {Url}",
                primaryManifest.Name,
                release.PortableUrl);

            return Task.FromResult(OperationResult<ContentManifest>.CreateSuccess(primaryManifest));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to resolve Generals Online manifest");
            return Task.FromResult(OperationResult<ContentManifest>.CreateFailure(
                $"Resolution failed: {ex.Message}"));
        }
    }

    private static DateTime? ParseVersionDate(string version)
    {
        try
        {
            var parts = version.Split(
                [GeneralsOnlineConstants.QfeSeparator],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (parts.Length < 1)
            {
                return null;
            }

            var datePart = parts[0];
            if (datePart.Length != 6)
            {
                return null;
            }

            if (!int.TryParse(datePart[..2], out var month) ||
                !int.TryParse(datePart.Substring(2, 2), out var day) ||
                !int.TryParse(datePart[4..], out var yearSuffix))
            {
                return null;
            }

            var year = 2000 + yearSuffix;
            return new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc);
        }
        catch
        {
            return null;
        }
    }
}
