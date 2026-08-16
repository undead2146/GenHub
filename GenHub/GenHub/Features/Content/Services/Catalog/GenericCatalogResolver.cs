using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Constants;
using GenHub.Core.Interfaces.Content;
using GenHub.Core.Interfaces.Manifest;
using GenHub.Core.Models.Enums;
using GenHub.Core.Models.Manifest;
using GenHub.Core.Models.Providers;
using GenHub.Core.Models.Results;
using GenHub.Core.Models.Results.Content;
using GenHub.Core.Services.Dependencies;
using Microsoft.Extensions.Logging;

namespace GenHub.Features.Content.Services.Catalog;

/// <summary>
/// Resolves GenHub-schema catalog items into <see cref="ContentManifest"/>s for install.
/// </summary>
/// <remarks>
/// Paired with <see cref="GenericCatalogDiscoverer"/> for any subscribed catalog — the modular
/// path that avoids per-publisher resolvers. Uses <see cref="IContentManifestBuilder"/> for
/// download, archive extraction, and CAS registration.
/// </remarks>
public class GenericCatalogResolver(
    ILogger<GenericCatalogResolver> logger,
    Func<IContentManifestBuilder> manifestBuilderFactory) : IContentResolver
{
    /// <inheritdoc />
    public string ResolverId => CatalogConstants.GenericCatalogResolverId;

    /// <inheritdoc />
    public async Task<OperationResult<ContentManifest>> ResolveAsync(
        ContentSearchResult searchResult,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(searchResult);

        try
        {
            // Extract catalog metadata from search result (stored as JSON strings)
            if (!searchResult.ResolverMetadata.TryGetValue(CatalogConstants.ReleaseJsonMetadataKey, out var releaseJson))
            {
                return OperationResult<ContentManifest>.CreateFailure("Missing release metadata");
            }

            if (!searchResult.ResolverMetadata.TryGetValue(CatalogConstants.CatalogItemJsonMetadataKey, out var contentItemJson))
            {
                return OperationResult<ContentManifest>.CreateFailure("Missing content item metadata");
            }

            if (!searchResult.ResolverMetadata.TryGetValue(CatalogConstants.PublisherProfileJsonMetadataKey, out var publisherJson))
            {
                return OperationResult<ContentManifest>.CreateFailure("Missing publisher profile");
            }

            // Deserialize from JSON
            var release = JsonSerializer.Deserialize<ContentRelease>(releaseJson);
            var contentItem = JsonSerializer.Deserialize<CatalogContentItem>(contentItemJson);
            var publisher = JsonSerializer.Deserialize<PublisherProfile>(publisherJson);

            if (release == null || contentItem == null || publisher == null)
            {
                return OperationResult<ContentManifest>.CreateFailure("Failed to deserialize catalog metadata");
            }

            logger.LogInformation(
                "Resolving content '{ContentName}' v{Version} from publisher '{PublisherId}'",
                contentItem.Name,
                release.Version,
                publisher.Id);

            var declaredPublisherId = CatalogManifestIdentity.ResolveDeclaredPublisherType(contentItem);

            var publisherDisplayName = !string.IsNullOrWhiteSpace(contentItem.Metadata?.Author)
                ? contentItem.Metadata.Author
                : publisher.Name;

            var website = !string.IsNullOrWhiteSpace(contentItem.Metadata?.DocumentationUrl)
                ? contentItem.Metadata.DocumentationUrl
                : (publisher.Website ?? string.Empty);

            // ContentBundle (and other meta-packages) may ship no downloadable artifacts —
            // their payload is the dependency graph alone. Skip remote-file registration.
            var primaryArtifact = release.Artifacts?.FirstOrDefault(a => a.IsPrimary)
                ?? release.Artifacts?.FirstOrDefault();

            var effectiveContentId = !string.IsNullOrWhiteSpace(primaryArtifact?.Variant)
                ? $"{contentItem.Id}-{primaryArtifact.Variant.Trim()}"
                : contentItem.Id;

            var resolvedName = !string.IsNullOrWhiteSpace(searchResult.Name)
                ? searchResult.Name
                : (!string.IsNullOrWhiteSpace(primaryArtifact?.Variant)
                    ? $"{contentItem.Name} ({primaryArtifact.Variant})"
                    : contentItem.Name);

            var resolvedTargetGame = contentItem.TargetGame;
            if (primaryArtifact?.VariantAxis?.Equals("game-type", StringComparison.OrdinalIgnoreCase) == true)
            {
                if (primaryArtifact.Variant?.Equals("Generals", StringComparison.OrdinalIgnoreCase) == true)
                {
                    resolvedTargetGame = GameType.Generals;
                }
                else if (primaryArtifact.Variant?.Equals("Zero Hour", StringComparison.OrdinalIgnoreCase) == true ||
                         primaryArtifact.Variant?.Equals("ZeroHour", StringComparison.OrdinalIgnoreCase) == true)
                {
                    resolvedTargetGame = GameType.ZeroHour;
                }
            }
            else if (searchResult.TargetGame != GameType.Unknown)
            {
                resolvedTargetGame = searchResult.TargetGame;
            }

            var builder = manifestBuilderFactory()
                .WithBasicInfo(declaredPublisherId, effectiveContentId, release.Version)
                .WithContentType(contentItem.ContentType, resolvedTargetGame)
                .WithName(resolvedName)
                .WithPublisher(
                    publisherDisplayName,
                    website,
                    publisher.SupportUrl ?? string.Empty,
                    publisher.ContactEmail ?? string.Empty,
                    publisherType: declaredPublisherId)
                .WithMetadata(
                    description: contentItem.Description,
                    tags: [.. contentItem.Tags],
                    iconUrl: contentItem.Metadata?.BannerUrl ?? string.Empty,
                    screenshotUrls: contentItem.Metadata?.ScreenshotUrls?.ToList(),
                    changelogUrl: contentItem.Metadata?.DocumentationUrl ?? string.Empty);

            if (primaryArtifact != null)
            {
                // Sanitize filename - some publishers use dynamic fetch URLs (e.g., fetch.aspx)
                // In these cases, derive a proper filename from the content name
                var filename = primaryArtifact.Filename;
                if (string.IsNullOrEmpty(filename) ||
                    filename.Contains("fetch.aspx", StringComparison.OrdinalIgnoreCase) ||
                    filename.Contains('?') ||
                    filename.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    // Use content name with appropriate extension based on MIME type
                    var extension = primaryArtifact.ContentType?.ToLowerInvariant() switch
                    {
                        "application/zip" => ".zip",
                        "application/x-rar-compressed" => ".rar",
                        "application/x-7z-compressed" => ".7z",
                        _ => Path.GetExtension(primaryArtifact.DownloadUrl) is { Length: > 1 } urlExt
                            ? urlExt
                            : ".zip",
                    };
                    filename = SanitizeFileName($"{Path.GetFileName(contentItem.Name)}{extension}");
                    logger.LogDebug(
                        "Sanitized filename from '{OriginalFilename}' to '{NewFilename}'",
                        primaryArtifact.Filename,
                        filename);
                }
                else
                {
                    filename = SanitizeFileName(Path.GetFileName(filename));
                }

                // Add remote file with download URL
                // The file will be downloaded during the delivery phase by the deliverer
                logger.LogDebug(
                    "Adding remote file {Filename} with download URL {Url}",
                    filename,
                    primaryArtifact.DownloadUrl);

                await builder.AddRemoteFileAsync(
                    relativePath: filename,
                    downloadUrl: primaryArtifact.DownloadUrl,
                    sourceType: ContentSourceType.RemoteDownload,
                    isExecutable: false,
                    permissions: null);
            }
            else
            {
                logger.LogInformation(
                    "Content '{ContentName}' has no downloadable artifacts (dependency-only package)",
                    contentItem.Name);
            }

            // Add dependencies
            foreach (var dependency in release.Dependencies)
            {
                var dependencyType = CatalogManifestIdentity.ResolveDependencyContentType(dependency, contentItem);

                // A GameInstallation dependency is a semantic type-only constraint ("needs the
                // base game"), not a reference to a real manifest. Emit the canonical foundation
                // dependency so DependencyResolver skips the pool lookup and GameClientProfileService
                // injects the user's concrete installation — matching every dedicated publisher.
                if (dependencyType == ContentType.GameInstallation ||
                    CatalogManifestIdentity.IsBaseGameDependency(dependency))
                {
                    var isGenerals = dependency.ContentId.Equals("generals", StringComparison.OrdinalIgnoreCase) ||
                                     resolvedTargetGame == GameType.Generals;
                    var foundation = isGenerals &&
                                     !dependency.ContentId.Equals("zerohour", StringComparison.OrdinalIgnoreCase)
                        ? BaseDependencyBuilder.CreateGenerals108Dependency()
                        : BaseDependencyBuilder.CreateZeroHour104Dependency();

                    builder.AddDependency(
                        id: foundation.Id,
                        name: foundation.Name,
                        dependencyType: ContentType.GameInstallation,
                        installBehavior: DependencyInstallBehavior.RequireExisting,
                        minVersion: dependency.VersionConstraint ?? foundation.MinVersion ?? string.Empty,
                        compatibleGameTypes: foundation.CompatibleGameTypes);
                    continue;
                }

                var dependencyId = CatalogManifestIdentity.CreateContentId(
                    dependency.PublisherId,
                    dependencyType,
                    dependency.ContentId,
                    dependency.VersionConstraint);

                var installBehavior = contentItem.ContentType == ContentType.ContentBundle && !dependency.IsOptional
                    ? DependencyInstallBehavior.AutoInstall
                    : dependency.IsOptional
                        ? DependencyInstallBehavior.Optional
                        : DependencyInstallBehavior.RequireExisting;

                builder.AddDependency(
                    id: ManifestId.Create(dependencyId),
                    name: dependency.ContentId,
                    dependencyType: dependencyType,
                    installBehavior: installBehavior,
                    minVersion: dependency.VersionConstraint ?? string.Empty);
            }

            var manifest = builder.Build();

            // Store primary artifact hash if present
            if (primaryArtifact != null && !string.IsNullOrWhiteSpace(primaryArtifact.Sha256))
            {
                var primaryFile = manifest.Files.FirstOrDefault();
                if (primaryFile != null)
                {
                    primaryFile.Hash = primaryArtifact.Sha256;
                }
            }

            // The discoverer already minted a unique 5-segment ID (including variant suffixes).
            // Keep that identity so pool lookups, ContentStateService, and bundle deps agree.
            if (!string.IsNullOrWhiteSpace(searchResult.Id) &&
                ManifestIdValidator.IsValid(searchResult.Id, out _))
            {
                manifest.Id = ManifestId.Create(searchResult.Id);
            }

            manifest.Name = resolvedName;
            manifest.OriginalProviderName = declaredPublisherId;
            manifest.OriginalContentId = searchResult.Id ?? contentItem.Id;

            foreach (var dep in manifest.Dependencies)
            {
                if (dep.DependencyType == ContentType.GameInstallation && dep.CompatibleGameTypes.Count == 0)
                {
                    dep.CompatibleGameTypes.Add(contentItem.TargetGame);
                }
            }

            // Add metadata to identify this manifest as coming from generic catalog
            manifest.Metadata.Description = contentItem.Description;
            manifest.Metadata.Tags = [.. contentItem.Tags];

            if (!string.IsNullOrWhiteSpace(primaryArtifact?.Variant))
            {
                var variantTag = $"variant:{primaryArtifact.Variant.ToLowerInvariant()}";
                if (!manifest.Metadata.Tags.Contains(variantTag, StringComparer.OrdinalIgnoreCase))
                {
                    manifest.Metadata.Tags.Add(variantTag);
                }
            }

            if (!manifest.Metadata.Tags.Any(t => t.StartsWith("contentCode:", StringComparison.OrdinalIgnoreCase)))
            {
                manifest.Metadata.Tags.Add($"contentCode:{contentItem.Id}");
            }

            logger.LogInformation(
                "Successfully resolved manifest for '{ContentName}' with {FileCount} files",
                manifest.Name,
                manifest.Files.Count);

            return OperationResult<ContentManifest>.CreateSuccess(manifest);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to resolve content from catalog");
            return OperationResult<ContentManifest>.CreateFailure($"Resolution failed: {ex.Message}");
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return "download.zip";
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));

        return string.IsNullOrWhiteSpace(sanitized) ? "download.zip" : sanitized;
    }
}
