using System;
using System.Collections.Generic;
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
/// Resolves a ContentSearchResult (from GenericCatalogDiscoverer) into a full ContentManifest.
/// </summary>
public class GenericCatalogResolver(
    ILogger<GenericCatalogResolver> logger,
    Func<IContentManifestBuilder> manifestBuilderFactory) : IContentResolver
{
    /// <inheritdoc />
    public string ResolverId => CatalogConstants.GenericCatalogResolverId;

    /// <inheritdoc />
    public async Task<OperationResult<ContentManifest>> ResolveAsync(
        ContentSearchResult discoveredItem,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(discoveredItem);

        try
        {
            // Extract catalog item and release metadata
            if (!discoveredItem.ResolverMetadata.TryGetValue(CatalogConstants.ReleaseJsonMetadataKey, out var releaseJson))
            {
                return OperationResult<ContentManifest>.CreateFailure("Missing release metadata");
            }

            if (!discoveredItem.ResolverMetadata.TryGetValue(CatalogConstants.CatalogItemJsonMetadataKey, out var contentItemJson))
            {
                return OperationResult<ContentManifest>.CreateFailure("Missing content item metadata");
            }

            if (!discoveredItem.ResolverMetadata.TryGetValue(CatalogConstants.PublisherProfileJsonMetadataKey, out var publisherJson))
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

            var resolvedName = ResolveManifestName(discoveredItem, contentItem, primaryArtifact);
            var resolvedTargetGame = ResolveTargetGame(contentItem, primaryArtifact, discoveredItem);

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
                var filename = SanitizeArtifactFilename(primaryArtifact, contentItem);
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

            AddDependencies(logger, builder, discoveredItem, release, contentItem, resolvedTargetGame);

            var manifest = builder.Build();

            ApplyManifestPostProcessing(
                manifest,
                contentItem,
                primaryArtifact,
                declaredPublisherId,
                resolvedName,
                discoveredItem.Id,
                resolvedTargetGame);

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

    private static string ResolveManifestName(
        ContentSearchResult searchResult,
        CatalogContentItem contentItem,
        ReleaseArtifact? primaryArtifact)
    {
        if (!string.IsNullOrWhiteSpace(searchResult.Name))
        {
            return searchResult.Name;
        }

        return !string.IsNullOrWhiteSpace(primaryArtifact?.Variant)
            ? $"{contentItem.Name} ({primaryArtifact.Variant})"
            : contentItem.Name;
    }

    private static GameType ResolveTargetGame(
        CatalogContentItem contentItem,
        ReleaseArtifact? primaryArtifact,
        ContentSearchResult searchResult)
    {
        if (primaryArtifact?.VariantAxis?.Equals("game-type", StringComparison.OrdinalIgnoreCase) == true)
        {
            if (primaryArtifact.Variant?.Equals("Generals", StringComparison.OrdinalIgnoreCase) == true)
            {
                return GameType.Generals;
            }

            if (primaryArtifact.Variant?.Equals("Zero Hour", StringComparison.OrdinalIgnoreCase) == true ||
                primaryArtifact.Variant?.Equals("ZeroHour", StringComparison.OrdinalIgnoreCase) == true)
            {
                return GameType.ZeroHour;
            }
        }

        if (searchResult.TargetGame != GameType.Unknown)
        {
            return searchResult.TargetGame;
        }

        return contentItem.TargetGame;
    }

    private static string SanitizeArtifactFilename(ReleaseArtifact primaryArtifact, CatalogContentItem contentItem)
    {
        var filename = primaryArtifact.Filename;
        if (string.IsNullOrEmpty(filename) ||
            filename.Contains("fetch.aspx", StringComparison.OrdinalIgnoreCase) ||
            filename.Contains('?') ||
            filename.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            var rawUrl = primaryArtifact.DownloadUrl;
            var queryIndex = rawUrl.IndexOfAny(['?', '#']);
            var cleanUrl = queryIndex >= 0 ? rawUrl[..queryIndex] : rawUrl;
            var extension = primaryArtifact.ContentType?.ToLowerInvariant() switch
            {
                "application/zip" => ".zip",
                "application/x-rar-compressed" => ".rar",
                "application/x-7z-compressed" => ".7z",
                _ => Path.GetExtension(cleanUrl) is { Length: > 1 } urlExt
                    ? urlExt
                    : ".zip",
            };
            return SanitizeFileName($"{Path.GetFileName(contentItem.Name)}{extension}");
        }

        return SanitizeFileName(Path.GetFileName(filename));
    }

    private static string SanitizeFileName(string filename)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return string.Concat(filename.Select(c => invalidChars.Contains(c) ? '_' : c));
    }

    private static void AddDependencies(
        ILogger logger,
        IContentManifestBuilder builder,
        ContentSearchResult discoveredItem,
        ContentRelease release,
        CatalogContentItem contentItem,
        GameType resolvedTargetGame)
    {
        var bundleComponents = TryDeserializeBundleComponents(logger, discoveredItem, contentItem.Id);

        foreach (var dependency in release.Dependencies)
        {
            var dependencyType = CatalogManifestIdentity.ResolveDependencyContentType(dependency, contentItem);

            if (dependencyType == ContentType.GameInstallation ||
                CatalogManifestIdentity.IsBaseGameDependency(dependency))
            {
                AddBaseGameDependency(builder, dependency, resolvedTargetGame);
                continue;
            }

            AddCatalogDependency(builder, dependency, contentItem, bundleComponents);
        }
    }

    private static List<CatalogBundleComponentDescriptor>? TryDeserializeBundleComponents(
        ILogger logger,
        ContentSearchResult discoveredItem,
        string contentItemId)
    {
        if (!discoveredItem.ResolverMetadata.TryGetValue(CatalogConstants.BundleComponentsJsonMetadataKey, out var bundleJson) ||
            string.IsNullOrWhiteSpace(bundleJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<List<CatalogBundleComponentDescriptor>>(bundleJson)
                ?? throw new JsonException("Bundle component metadata deserialized to null.");
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to deserialize bundle component metadata for '{ContentId}'", contentItemId);
            throw new InvalidOperationException($"Bundle component metadata for '{contentItemId}' is invalid.", ex);
        }
    }

    private static void AddBaseGameDependency(
        IContentManifestBuilder builder,
        CatalogDependency dependency,
        GameType resolvedTargetGame)
    {
        var isGenerals = dependency.ContentId.Equals("generals", StringComparison.OrdinalIgnoreCase) ||
                         resolvedTargetGame == GameType.Generals;
        var foundation = isGenerals &&
                         !dependency.ContentId.Equals("zerohour", StringComparison.OrdinalIgnoreCase)
            ? BaseDependencyBuilder.CreateGenerals108Dependency()
            : BaseDependencyBuilder.CreateZeroHour104Dependency();

        var baseMinVersion = !string.IsNullOrWhiteSpace(dependency.VersionConstraint)
            ? CatalogManifestIdentity.StripVersionConstraint(dependency.VersionConstraint)
            : foundation.MinVersion ?? string.Empty;

        builder.AddDependency(
            id: foundation.Id,
            name: foundation.Name,
            dependencyType: ContentType.GameInstallation,
            installBehavior: DependencyInstallBehavior.RequireExisting,
            minVersion: baseMinVersion,
            compatibleGameTypes: foundation.CompatibleGameTypes);
    }

    private static void AddCatalogDependency(
        IContentManifestBuilder builder,
        CatalogDependency dependency,
        CatalogContentItem contentItem,
        List<CatalogBundleComponentDescriptor>? bundleComponents)
    {
        var (depPublisherId, depVersion, dependencyType) = ResolveDependencyIdentity(dependency, contentItem, bundleComponents);

        var dependencyId = CatalogManifestIdentity.CreateContentId(
            depPublisherId,
            dependencyType,
            dependency.ContentId,
            depVersion);

        var (minVersion, maxVersion, compatibleVersions) = ParseVersionConstraint(dependency.VersionConstraint);

        var installBehavior = DependencyInstallBehavior.RequireExisting;
        if (dependency.IsOptional)
        {
            installBehavior = DependencyInstallBehavior.Optional;
        }
        else if (contentItem.ContentType == ContentType.ContentBundle)
        {
            installBehavior = DependencyInstallBehavior.AutoInstall;
        }

        builder.AddDependency(
            id: ManifestId.Create(dependencyId),
            name: dependency.ContentId,
            dependencyType: dependencyType,
            installBehavior: installBehavior,
            minVersion: minVersion,
            maxVersion: maxVersion,
            compatibleVersions: compatibleVersions);
    }

    private static (string PublisherId, string Version, ContentType DependencyType) ResolveDependencyIdentity(
        CatalogDependency dependency,
        CatalogContentItem contentItem,
        List<CatalogBundleComponentDescriptor>? bundleComponents)
    {
        var cleanConstraint = CatalogManifestIdentity.StripVersionConstraint(dependency.VersionConstraint);
        var depPublisherId = dependency.PublisherId;
        var depVersion = cleanConstraint;
        var dependencyType = CatalogManifestIdentity.ResolveDependencyContentType(dependency, contentItem);

        if (bundleComponents?.FirstOrDefault(c => string.Equals(c.ContentId, dependency.ContentId, StringComparison.OrdinalIgnoreCase)) is { } matched)
        {
            if (!string.IsNullOrWhiteSpace(matched.PublisherId))
            {
                depPublisherId = matched.PublisherId;
            }

            if (!string.IsNullOrWhiteSpace(matched.ReleaseVersion))
            {
                depVersion = matched.ReleaseVersion;
            }

            if (!string.IsNullOrWhiteSpace(matched.ContentType) &&
                Enum.TryParse<ContentType>(matched.ContentType, true, out var matchedType))
            {
                dependencyType = matchedType;
            }
        }

        return (depPublisherId, depVersion, dependencyType);
    }

    private static (string MinVersion, string MaxVersion, List<string>? CompatibleVersions) ParseVersionConstraint(string? constraint)
    {
        if (string.IsNullOrWhiteSpace(constraint))
        {
            return (string.Empty, string.Empty, null);
        }

        var trimmed = constraint.Trim();
        if (trimmed.Equals("latest", StringComparison.OrdinalIgnoreCase))
        {
            return (string.Empty, string.Empty, null);
        }

        if (trimmed.Contains(',') || trimmed.Contains('|'))
        {
            return ParseListConstraint(trimmed);
        }

        return ParseRangedTokens(trimmed);
    }

    private static (string MinVersion, string MaxVersion, List<string>? CompatibleVersions) ParseListConstraint(string trimmed)
    {
        var parts = trimmed.Split([',', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(CatalogManifestIdentity.StripVersionConstraint)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return (string.Empty, string.Empty, parts.Count > 0 ? parts : null);
    }

    private static (string MinVersion, string MaxVersion, List<string>? CompatibleVersions) ParseRangedTokens(string trimmed)
    {
        var tokens = trimmed.Split([' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 1 && IsExactVersion(tokens[0]))
        {
            var stripped = CatalogManifestIdentity.StripVersionConstraint(tokens[0]);
            if (!string.IsNullOrWhiteSpace(stripped))
            {
                return (stripped, stripped, [stripped]);
            }
        }

        string minVersion = string.Empty;
        string maxVersion = string.Empty;

        foreach (var token in tokens)
        {
            ApplyTokenBound(token, ref minVersion, ref maxVersion);
        }

        return (minVersion, maxVersion, null);
    }

    private static bool IsExactVersion(string token)
    {
        return !token.StartsWith('>') &&
               !token.StartsWith('<') &&
               !token.StartsWith('^') &&
               !token.StartsWith('~');
    }

    private static void ApplyTokenBound(string token, ref string minVersion, ref string maxVersion)
    {
        if (token.StartsWith(">=", StringComparison.Ordinal) || token.StartsWith('>'))
        {
            minVersion = CatalogManifestIdentity.StripVersionConstraint(token);
        }
        else if (token.StartsWith("<=", StringComparison.Ordinal) || token.StartsWith('<'))
        {
            maxVersion = CatalogManifestIdentity.StripVersionConstraint(token);
        }
        else if (token.StartsWith('^'))
        {
            var target = CatalogManifestIdentity.StripVersionConstraint(token);
            minVersion = target;
            var parts = target.Split('.');
            if (parts.Length > 0 && int.TryParse(parts[0], out var major))
            {
                maxVersion = $"{major + 1}.0.0";
            }
        }
        else if (token.StartsWith('~'))
        {
            var target = CatalogManifestIdentity.StripVersionConstraint(token);
            minVersion = target;
            var parts = target.Split('.');
            if (parts.Length >= 2 && int.TryParse(parts[0], out var major) && int.TryParse(parts[1], out var minor))
            {
                maxVersion = $"{major}.{minor + 1}.0";
            }
        }
    }

    private static void ApplyManifestPostProcessing(
        ContentManifest manifest,
        CatalogContentItem contentItem,
        ReleaseArtifact? primaryArtifact,
        string declaredPublisherId,
        string resolvedName,
        string? searchResultId,
        GameType resolvedTargetGame)
    {
        if (primaryArtifact != null && !string.IsNullOrWhiteSpace(primaryArtifact.Sha256))
        {
            var primaryFile = manifest.Files.FirstOrDefault();
            if (primaryFile != null)
            {
                primaryFile.Hash = primaryArtifact.Sha256;
            }
        }

        if (!string.IsNullOrWhiteSpace(searchResultId) &&
            ManifestIdValidator.IsValid(searchResultId, out _))
        {
            manifest.Id = ManifestId.Create(searchResultId);
        }

        manifest.Name = resolvedName;
        manifest.OriginalProviderName = declaredPublisherId;
        manifest.OriginalContentId = searchResultId ?? contentItem.Id;

        foreach (var dep in manifest.Dependencies)
        {
            if (dep.DependencyType == ContentType.GameInstallation && dep.CompatibleGameTypes.Count == 0)
            {
                dep.CompatibleGameTypes.Add(resolvedTargetGame);
            }
        }

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

        if (manifest.Metadata.Tags.All(t => !t.StartsWith("contentCode:", StringComparison.OrdinalIgnoreCase)))
        {
            manifest.Metadata.Tags.Add($"contentCode:{contentItem.Id}");
        }
    }
}
